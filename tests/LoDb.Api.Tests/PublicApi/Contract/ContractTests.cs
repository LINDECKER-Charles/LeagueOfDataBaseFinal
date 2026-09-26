using System.Text.Json.Nodes;
using LoDb.Api.Tests.PublicApi.Support;

namespace LoDb.Api.Tests.PublicApi.Contract;

/// <summary>
/// Replays each group go-api was recorded on (L6.1) against the new <c>/v1</c>, on the same
/// data set, and compares every answer as the capture normalized go-api's: status, headers,
/// body.
/// </summary>
/// <remarks>
/// A group runs on a copy of the data set of its own, with a fresh API, as the capture
/// restarted go-api for each. Every difference of a group is reported at once.
/// </remarks>
[Collection(V1Group.Name)]
public sealed class ContractTests(V1Server server)
{
    private const string CorsPrefix = "access-control-";

    [Theory]
    [InlineData("01-auth")]
    [InlineData("02-plans")]
    [InlineData("03-profiles")]
    [InlineData("04-builds")]
    [InlineData("05-trends")]
    [InlineData("06-limits")]
    [InlineData("07-billing")]
    [InlineData("08-routing")]
    [InlineData("09-key-cache")]
    [InlineData("10-outage")]
    public async Task AnswersAsRecordedSaveTheListedDepartures(string group)
    {
        var exchanges = RecordedExchange.LoadGroup(group);
        Assert.Contains(exchanges, static exchange => exchange.Request is not null);
        var differences = new List<string>();
        await using (var run = await server.StartAsync())
        {
            foreach (var exchange in exchanges)
            {
                if (exchange.Action is not null)
                {
                    await PerformAsync(run, exchange);
                    continue;
                }

                var actual = await SendAsync(run, exchange.Request!);
                if (ContractDeviations.IsOutsideContract(exchange.Request!))
                {
                    var cors = actual.Headers.Keys
                        .Where(static name => name.StartsWith(CorsPrefix, StringComparison.Ordinal))
                        .ToList();
                    differences.AddRange(cors.Select(name => $"{exchange.Id}: {name} outside /v1"));
                    continue;
                }

                var expected = await ExpectedAsync(run, group, exchange, exchanges, differences);
                differences.AddRange(
                    expected.DifferencesFrom(actual).Select(line => $"{exchange.Id}: {line}"));
            }
        }

        Assert.True(differences.Count == 0, $"{group}\n{string.Join('\n', differences)}");
    }

    private static async Task PerformAsync(V1Run run, RecordedExchange exchange)
    {
        switch (exchange.Action)
        {
            case "sleep":
                await run.SleepAsync(TimeSpan.FromMilliseconds(exchange.Milliseconds!.Value));
                break;
            case "sql":
                await run.ChangeKeysAsync(exchange.Sql!);
                break;
            case "stopDatabase":
                await run.Database.StopAsync();
                break;
            case "hideStorage":
                run.HideStorage();
                break;
            default:
                throw new InvalidOperationException($"{exchange.Id}: no action {exchange.Action}.");
        }
    }

    // The request as the capture sent it: method, path and headers untouched, keys resolved.
    private static async Task<RecordedResponse> SendAsync(V1Run run, RecordedRequest recorded)
    {
        using var request = new HttpRequestMessage(
            new HttpMethod(recorded.Method),
            new Uri(V1Fixtures.Resolve(recorded.Path), UriKind.Relative));
        foreach (var (name, value) in recorded.Headers)
        {
            request.Headers.TryAddWithoutValidation(name, V1Fixtures.Resolve(value));
        }

        var started = run.Clock.GetUtcNow();
        using var response = await run.Client.SendAsync(request, V1Server.Token);
        return await RecordedResponse.ReadAsync(response, started, run.Clock.GetUtcNow());
    }

    // go-api's answer, a listed departure, or a page of builds worked out by the oracle once
    // the oracle gives go-api's page as it was recorded.
    private static async Task<RecordedResponse> ExpectedAsync(
        V1Run run,
        string group,
        RecordedExchange exchange,
        IReadOnlyList<RecordedExchange> exchanges,
        List<string> differences)
    {
        if (ContractDeviations.Replace(group, exchange, exchanges) is { } departure)
        {
            return departure;
        }

        var recorded = exchange.Response!;
        if (recorded.Body?["pagination"] is not JsonObject pagination)
        {
            return recorded;
        }

        var championId = recorded.Body["champion_id"]!.GetValue<string>();
        var page = pagination["page"]!.GetValue<long>();
        var perPage = pagination["per_page"]!.GetValue<int>();
        var goApiPage = await BuildsOracle.PageAsync(
            run.Database,
            championId,
            page,
            perPage,
            bannedAuthorsListed: true);
        if (!JsonNode.DeepEquals(goApiPage, recorded.Body))
        {
            differences.Add($"{exchange.Id}: the oracle does not give go-api's page");
        }

        return recorded with
        {
            Body = await BuildsOracle.PageAsync(
                run.Database,
                championId,
                page,
                perPage,
                bannedAuthorsListed: false),
        };
    }
}
