using System.Diagnostics;
using System.Net;
using LoDb.Api.Tests.Accounts.Support;
using LoDb.Api.Tests.Profiles.Support;

namespace LoDb.Api.Tests.Profiles;

/// <summary>
/// A flood of erasures confirmed with a wrong password: the API hashes one password at a
/// time, refuses the surplus at once with a 503 and its <c>Retry-After</c>, and keeps
/// serving the catalog as fast as before.
/// </summary>
[Collection(ProfilesGroup.Name)]
public sealed class DeletionFloodTests(ProfilesApp app)
{
    private const int Accounts = 40;
    private const int AttemptsPerAccount = 5;
    private const int Attempts = Accounts * AttemptsPerAccount;

    // One permit and a queue of eight: at most nine requests are admitted, the others are
    // refused. An account whose five attempts were all admitted is locked by the fifth.
    private const int MostAdmitted = 1 + 8;
    private const double RetryAfterSeconds = 5;

    private const int CatalogSamples = 20;
    private const int SlowdownFactor = 3;
    private const double Percentile = 0.99;
    private static readonly TimeSpan Jitter = TimeSpan.FromSeconds(1);

    private static readonly HttpStatusCode[] Answers =
    [
        HttpStatusCode.BadRequest,
        HttpStatusCode.TooManyRequests,
        HttpStatusCode.ServiceUnavailable,
    ];

    private static string CatalogPath =>
        $"/api/catalog/{ProfilesApp.Latest.Value}/en_US/champions";

    [Fact]
    public async Task WrongPasswordsHashOneAtATimeAndTheCatalogStaysFast()
    {
        var browsers = await SignInAsync();
        try
        {
            using var reader = app.Browser();
            var before = await ReadCatalogAsync(reader, Task.CompletedTask);
            app.Hashing.Reset();

            var flood = Task.WhenAll(browsers.SelectMany(static browser =>
                Enumerable.Range(0, AttemptsPerAccount).Select(_ => DeleteAsync(browser))));
            var during = await ReadCatalogAsync(reader, flood);
            var answers = await flood;

            Assert.Equal(1, app.Hashing.MostAtOnce);
            Assert.All(answers, static answer => Assert.Contains(answer.Status, Answers));
            var busy = answers
                .Where(static answer => answer.Status == HttpStatusCode.ServiceUnavailable)
                .ToList();
            Assert.InRange(busy.Count, Attempts - MostAdmitted, Attempts - 1);
            Assert.All(busy, static answer => Assert.Equal(RetryAfterSeconds, answer.RetryAfter));
            Assert.InRange(P99(during), TimeSpan.Zero, (P99(before) * SlowdownFactor) + Jitter);
        }
        finally
        {
            browsers.ForEach(static browser => browser.Dispose());
        }
    }

    private async Task<List<BrowserClient>> SignInAsync()
    {
        var browsers = new List<BrowserClient>(Accounts);
        for (var account = 0; account < Accounts; account++)
        {
            browsers.Add(await app.SignInAsync(await app.SeedAsync()));
        }

        return browsers;
    }

    private static async Task<(HttpStatusCode Status, double? RetryAfter)> DeleteAsync(
        BrowserClient browser)
    {
        using var response = await browser.PostAsync(ProfileCalls.Delete, new { password = "x" });
        return (response.StatusCode, response.Headers.RetryAfter?.Delta?.TotalSeconds);
    }

    // At least CatalogSamples reads, and as many as the flood lasts.
    private static async Task<List<TimeSpan>> ReadCatalogAsync(
        BrowserClient reader,
        Task flood)
    {
        var latencies = new List<TimeSpan>();
        while (latencies.Count < CatalogSamples || !flood.IsCompleted)
        {
            var watch = Stopwatch.StartNew();
            using var response = await reader.GetAsync(CatalogPath);
            latencies.Add(watch.Elapsed);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        return latencies;
    }

    private static TimeSpan P99(List<TimeSpan> latencies)
    {
        var sorted = latencies.Order().ToList();
        return sorted[(int)Math.Ceiling(Percentile * sorted.Count) - 1];
    }
}
