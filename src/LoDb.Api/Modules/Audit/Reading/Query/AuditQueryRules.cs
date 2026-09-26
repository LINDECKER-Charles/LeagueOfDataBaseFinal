using LoDb.Api.Modules.Accounts.Http;
using LoDb.Api.Modules.Audit.Http;
using LoDb.Api.Modules.Audit.Vocabulary;
using LoDb.Infrastructure.Audit;

namespace LoDb.Api.Modules.Audit.Reading.Query;

/// <summary>
/// Reads the filters and the page of a journal query, collecting the invalid ones as field
/// errors answered together.
/// </summary>
internal static class AuditQueryRules
{
    public const string UnknownAction = "unknown-action";
    public const string UnknownCategory = "unknown-category";
    public const string UnknownOutcome = "unknown-outcome";
    public const string UnknownActorType = "unknown-actor-type";
    public const string InvalidRange = "invalid-range";
    public const string OutOfRange = "out-of-range";

    /// <summary>
    /// The query <paramref name="request"/> describes; null once errors are added.
    /// </summary>
    public static AuditQuery? Parse(AuditQueryRequest request, FieldErrors errors)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(errors);
        var actions = Actions(request, errors);
        if (!TryOptional(request.Outcome, AuditVocabulary.ParseOutcome, out var outcome))
        {
            errors.Add("outcome", UnknownOutcome);
        }

        if (!TryOptional(request.ActorType, AuditVocabulary.ParseActorType, out var actorType))
        {
            errors.Add("actorType", UnknownActorType);
        }

        if (request is { From: { } from, To: { } to } && from > to)
        {
            errors.Add("to", InvalidRange);
        }

        var window = Window(request, errors);
        return errors.IsEmpty
            ? new AuditQuery(Filter(request, actions) with
            {
                Outcome = outcome,
                ActorType = actorType,
            }, window)
            : null;
    }

    private static AuditFilter Filter(AuditQueryRequest request, List<AuditAction>? actions)
    {
        var actor = request.Actor?.Trim();
        return new AuditFilter
        {
            Actions = actions,
            ActorId = request.ActorId,
            Actor = string.IsNullOrEmpty(actor) ? null : actor,
            From = StartOf(request.From),
            Until = StartOf(request.To?.AddDays(1)),
        };
    }

    private static List<AuditAction>? Actions(AuditQueryRequest request, FieldErrors errors)
    {
        List<AuditAction>? actions = null;
        foreach (var code in request.Action ?? [])
        {
            if (AuditCodes.TryParse(code, AuditVocabulary.ParseAction, out var action))
            {
                (actions ??= []).Add(action);
            }
            else
            {
                errors.Add("action", UnknownAction);
            }
        }

        if (string.IsNullOrEmpty(request.Category))
        {
            return actions;
        }

        var group = AuditCategories.ActionsOf(request.Category);
        if (group.Count == 0)
        {
            errors.Add("category", UnknownCategory);
            return actions;
        }

        // Both given: an action outside the group matches nothing.
        return actions is null ? [.. group] : [.. actions.Intersect(group)];
    }

    private static PageWindow Window(AuditQueryRequest request, FieldErrors errors)
    {
        var page = request.Page ?? 1;
        var size = request.PageSize ?? PageWindow.DefaultPageSize;
        if (page < 1)
        {
            errors.Add("page", OutOfRange);
        }

        if (size is < 1 or > PageWindow.MaxPageSize)
        {
            errors.Add("pageSize", OutOfRange);
        }

        return new PageWindow(page, size);
    }

    // True with no value for an absent code; false for an unknown one.
    private static bool TryOptional<T>(string? code, Func<string, T> parse, out T? value)
        where T : struct
    {
        value = null;
        if (string.IsNullOrEmpty(code))
        {
            return true;
        }

        if (!AuditCodes.TryParse(code, parse, out var parsed))
        {
            return false;
        }

        value = parsed;
        return true;
    }

    private static DateTimeOffset? StartOf(DateOnly? day) =>
        day is { } date
            ? new DateTimeOffset(date.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero)
            : null;
}
