using LoDb.Ingestion.Egress.Errors;

namespace LoDb.Ingestion.Egress.Http;

/// <summary>
/// A response body that enforces the size cap and the read deadline, and speaks the egress
/// exceptions only. Disposing it releases the response and its connection.
/// </summary>
/// <remarks>
/// Needed because the body is read after the resilience pipeline has returned: the attempt
/// timeout stops at the headers, and nothing else would bound a stalled body.
/// </remarks>
internal sealed class BoundedResponseStream : Stream
{
    private readonly HttpResponseMessage response;
    private readonly Stream body;
    private readonly long maxBytes;
    private readonly CancellationTokenSource deadline;
    private long received;

    public BoundedResponseStream(HttpResponseMessage response, Stream body, BodyPolicy policy)
    {
        this.response = response;
        this.body = body;
        maxBytes = policy.MaxBytes;
        deadline = new CancellationTokenSource(policy.ReadTimeout, policy.Time);
    }

    public override bool CanRead => true;

    public override bool CanSeek => false;

    public override bool CanWrite => false;

    public override long Length => throw new NotSupportedException();

    public override long Position
    {
        get => received;
        set => throw new NotSupportedException();
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        ValidateBufferArguments(buffer, offset, count);
        return Read(buffer.AsSpan(offset, count));
    }

    public override int Read(Span<byte> buffer)
    {
        try
        {
            return Count(body.Read(buffer));
        }
        catch (Exception exception) when (IsTransport(exception))
        {
            throw Broken(exception);
        }
    }

    public override Task<int> ReadAsync(
        byte[] buffer,
        int offset,
        int count,
        CancellationToken cancellationToken)
    {
        ValidateBufferArguments(buffer, offset, count);
        return ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();
    }

    public override async ValueTask<int> ReadAsync(
        Memory<byte> buffer,
        CancellationToken cancellationToken = default)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken, deadline.Token);
        try
        {
            return Count(await body.ReadAsync(buffer, linked.Token).ConfigureAwait(false));
        }
        catch (OperationCanceledException exception)
            when (!cancellationToken.IsCancellationRequested)
        {
            throw new EgressTransientException("Response body timed out.", exception);
        }
        catch (Exception exception) when (IsTransport(exception))
        {
            throw Broken(exception);
        }
    }

    public override void Flush()
    {
    }

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) =>
        throw new NotSupportedException();

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            body.Dispose();
            response.Dispose();
            deadline.Dispose();
        }

        base.Dispose(disposing);
    }

    private static bool IsTransport(Exception exception) =>
        exception is IOException or HttpRequestException;

    private static EgressTransientException Broken(Exception exception) =>
        new("Response body interrupted.", exception);

    // The chunk that crosses the cap throws: the caller never mistakes a prefix for the body.
    private int Count(int read)
    {
        received += read;
        return received > maxBytes ? throw new EgressBodyTooLargeException(maxBytes) : read;
    }
}
