using System.Net;
using LoDb.Ingestion.Egress;
using LoDb.Ingestion.Egress.Errors;

namespace LoDb.Ingestion.Tests.Egress;

public sealed class OutcomeTests
{
    [Fact]
    public async Task SuccessIsPresentWithItsBodyAndType()
    {
        using var harness = EgressHarness.Create(
            FakeUpstream.Answering(HttpStatusCode.OK, "[\"15.1.1\"]"));

        var outcome = await harness.FetchAsync(EgressHarness.DdragonUrl);

        var present = Assert.IsType<FetchOutcome.Present>(outcome);
        await using (present.Content)
        {
            Assert.StartsWith("application/json", present.ContentType, StringComparison.Ordinal);
            Assert.Equal("[\"15.1.1\"]", await new StreamReader(present.Content).ReadToEndAsync(
                TestContext.Current.CancellationToken));
        }
    }

    // A definitive absence is a value, answered at once: retrying a 403 would only slow the
    // ingestion of the 180 versions whose runes are forbidden.
    [Theory]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.NotFound)]
    public async Task ForbiddenOrNotFoundIsAbsent(HttpStatusCode status)
    {
        var upstream = FakeUpstream.Answering(status);
        using var harness = EgressHarness.Create(upstream);

        var outcome = await harness.FetchAsync(EgressHarness.DdragonUrl);

        Assert.Equal(new FetchOutcome.Absent((int)status), outcome);
        Assert.Equal(1, upstream.Calls);
    }

    [Theory]
    [InlineData(HttpStatusCode.Gone)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.Unauthorized)]
    public async Task OtherStatusIsTransientWithoutRetry(HttpStatusCode status)
    {
        var upstream = FakeUpstream.Answering(status);
        using var harness = EgressHarness.Create(upstream);

        var failure = await Assert.ThrowsAsync<EgressTransientException>(
            () => harness.FetchAsync(EgressHarness.DdragonUrl));

        Assert.Equal(status, failure.StatusCode);
        Assert.Equal(1, upstream.Calls);
    }
}
