namespace LoDb.Testing.Fixtures;

/// <summary>
/// A request the recording has no answer for.
/// </summary>
/// <remarks>
/// Not an <see cref="HttpRequestException"/> on purpose: the egress zone would take it for a
/// network failure, retry it, then report it as transient, and a test expecting a transient
/// failure would pass for the wrong reason.
/// </remarks>
public sealed class UnrecordedFixtureException : InvalidOperationException
{
    public UnrecordedFixtureException()
    {
    }

    public UnrecordedFixtureException(string message)
        : base(message)
    {
    }

    public UnrecordedFixtureException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    public UnrecordedFixtureException(Uri url)
        : base(
            $"No recorded answer for {url}. Add it to tools/fixtures/lib/recording-plan.mjs "
            + "and run `node tools/fixtures/record.mjs`.")
    {
        Url = url;
    }

    public Uri? Url { get; }
}
