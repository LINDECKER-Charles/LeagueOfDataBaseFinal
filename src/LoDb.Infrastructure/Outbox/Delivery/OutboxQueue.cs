using LoDb.Infrastructure.Persistence.Outbox;
using Microsoft.Extensions.Options;
using Npgsql;

namespace LoDb.Infrastructure.Outbox.Delivery;

/// <summary>
/// The SQL of the worker on <c>email_outbox</c>: take a batch, then record each outcome.
/// </summary>
/// <remarks>
/// <para>
/// Taking a batch is one statement: <c>FOR UPDATE SKIP LOCKED</c> keeps two instances off
/// the same rows, and the lease written with the attempt keeps them off afterwards, without
/// a transaction held open while the relay answers. An outcome is only recorded if the row
/// is still at the attempt this instance took, so that an instance whose lease ran out
/// cannot overwrite the next one's.
/// </para>
/// <para>
/// The hourly caps count the rows sent within the hour, only once some message is due: an
/// idle look reads the index of the pending rows alone. Two instances sending at once may
/// together pass a cap by a batch, which the caps' margin absorbs.
/// </para>
/// </remarks>
internal sealed class OutboxQueue(
    NpgsqlDataSource dataSource,
    IOptions<OutboxOptions> options,
    TimeProvider timeProvider)
{
    // Each template's share is taken first in its own order, then the batch in the global one.
    private const string ClaimSql = """
        UPDATE email_outbox AS o
        SET attempts = o.attempts + 1, last_attempt_at = @now, next_attempt_at = @lease_until
        WHERE o.id IN (
            SELECT e.id FROM email_outbox AS e
            WHERE e.status = @pending AND e.next_attempt_at <= @now
              AND e.id IN (
                  SELECT share.id
                  FROM unnest(@templates, @allowances) AS allowance(template, permitted)
                  CROSS JOIN LATERAL (
                      SELECT d.id FROM email_outbox AS d
                      WHERE d.status = @pending AND d.next_attempt_at <= @now
                        AND d.template = allowance.template
                      ORDER BY d.next_attempt_at, d.id
                      LIMIT allowance.permitted) AS share)
            ORDER BY e.next_attempt_at, e.id
            LIMIT @batch_size
            FOR UPDATE OF e SKIP LOCKED)
        RETURNING o.id, o.recipient, o.template, o.locale, o.model::text, o.attempts
        """;

    private const string LoadsSql = """
        SELECT due.template, due.waiting,
               (SELECT count(*) FROM email_outbox AS s
                WHERE s.template = due.template AND s.status = @sent AND s.sent_at > @since)
        FROM (
            SELECT template, count(*) AS waiting FROM email_outbox
            WHERE status = @pending AND next_attempt_at <= @now
            GROUP BY template) AS due
        """;

    private const string SentSql = """
        UPDATE email_outbox
        SET status = @sent, sent_at = @now, last_error_code = NULL
        WHERE id = @id AND attempts = @attempts AND status = @pending
        """;

    private const string FailedSql = """
        UPDATE email_outbox
        SET status = @status, next_attempt_at = @next_attempt_at, last_error_code = @code
        WHERE id = @id AND attempts = @attempts AND status = @pending
        """;

    private const string PendingSql = "SELECT count(*) FROM email_outbox WHERE status = @pending";

    private static readonly string Pending = OutboxColumns.ToText(EmailOutboxStatus.Pending);
    private static readonly string Sent = OutboxColumns.ToText(EmailOutboxStatus.Sent);

    /// <summary>
    /// The templates with messages due now, and what each sent within the caps' window.
    /// </summary>
    public async Task<IReadOnlyList<TemplateLoad>> LoadsAsync(CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        await using var command = dataSource.CreateCommand(LoadsSql);
        command.Parameters.AddWithValue("now", now);
        command.Parameters.AddWithValue("since", now - OutboxHourlyCaps.Window);
        command.Parameters.AddWithValue("pending", Pending);
        command.Parameters.AddWithValue("sent", Sent);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var loads = new List<TemplateLoad>();
        while (await reader.ReadAsync(cancellationToken))
        {
            loads.Add(new TemplateLoad(
                reader.GetString(0),
                reader.GetInt64(1),
                reader.GetInt64(2)));
        }

        return loads;
    }

    /// <summary>Takes the due messages <paramref name="budget"/> allows, a batch at most.</summary>
    public async Task<IReadOnlyList<ClaimedMessage>> ClaimAsync(
        SendingBudget budget,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(budget);
        var now = timeProvider.GetUtcNow();
        await using var command = dataSource.CreateCommand(ClaimSql);
        command.Parameters.AddWithValue("now", now);
        command.Parameters.AddWithValue("lease_until", now + options.Value.Lease);
        command.Parameters.AddWithValue("pending", Pending);
        command.Parameters.AddWithValue("batch_size", options.Value.BatchSize);
        command.Parameters.AddWithValue("templates", budget.Templates.ToArray());
        command.Parameters.AddWithValue("allowances", budget.Allowances.ToArray());
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await ReadClaimedAsync(reader, cancellationToken);
    }

    public async Task MarkSentAsync(ClaimedMessage message, CancellationToken cancellationToken)
    {
        await using var command = dataSource.CreateCommand(SentSql);
        command.Parameters.AddWithValue("sent", Sent);
        command.Parameters.AddWithValue("now", timeProvider.GetUtcNow());
        AddRow(command, message);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task MarkFailedAsync(
        ClaimedMessage message,
        FailedAttempt attempt,
        CancellationToken cancellationToken)
    {
        var status = attempt.Dead ? EmailOutboxStatus.Dead : EmailOutboxStatus.Pending;
        await using var command = dataSource.CreateCommand(FailedSql);
        command.Parameters.AddWithValue("status", OutboxColumns.ToText(status));
        command.Parameters.AddWithValue("next_attempt_at", attempt.NextAttemptAt);
        command.Parameters.AddWithValue("code", attempt.ErrorCode);
        AddRow(command, message);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    /// <summary>Messages not yet sent nor given up: the depth of the queue.</summary>
    public async Task<long> CountPendingAsync(CancellationToken cancellationToken)
    {
        await using var command = dataSource.CreateCommand(PendingSql);
        command.Parameters.AddWithValue("pending", Pending);
        return (long)(await command.ExecuteScalarAsync(cancellationToken))!;
    }

    private static async Task<IReadOnlyList<ClaimedMessage>> ReadClaimedAsync(
        NpgsqlDataReader reader,
        CancellationToken cancellationToken)
    {
        var claimed = new List<ClaimedMessage>();
        while (await reader.ReadAsync(cancellationToken))
        {
            claimed.Add(new ClaimedMessage(
                reader.GetInt64(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetString(4),
                reader.GetInt32(5)));
        }

        // Oldest first, as they were chosen: RETURNING keeps no order of its own.
        return [.. claimed.OrderBy(static message => message.Id)];
    }

    private static void AddRow(NpgsqlCommand command, ClaimedMessage message)
    {
        command.Parameters.AddWithValue("id", message.Id);
        command.Parameters.AddWithValue("attempts", message.Attempts);
        command.Parameters.AddWithValue("pending", Pending);
    }
}
