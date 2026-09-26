using System.Text.Json;
using System.Text.Json.Nodes;
using LoDb.Desktop.Bridge;
using LoDb.Desktop.Bridge.Commands;
using LoDb.Desktop.Bridge.Validation;
using LoDb.Desktop.Lifecycle;
using LoDb.Desktop.Tests.Support;
using Microsoft.Extensions.Logging.Abstractions;

namespace LoDb.Desktop.Tests.Bridge;

public sealed class DesktopBridgeTests : IDisposable
{
    private const string JsonBase64 = "eyJhIjoxfQ==";

    private readonly FakeShell _shell = new();
    private readonly FakeBrowser _browser = new();
    private readonly TestFolder _folder = new();
    private readonly DesktopBridge _bridge;

    public DesktopBridgeTests()
    {
        var updates = new NoDesktopUpdates();
        _bridge = new DesktopBridge(
            [
                new OpenExternalCommand(_browser),
                new SaveFileCommand(_shell, NullLogger<SaveFileCommand>.Instance),
                new UpdateStateCommand(updates),
                new ApplyUpdateCommand(updates),
            ],
            NullLogger<DesktopBridge>.Instance);
    }

    public void Dispose() => _folder.Dispose();

    [Fact]
    public async Task AnswersWithTheIdOfTheRequest()
    {
        var reply = await SendAsync(new { id = "r-1", type = "updateState", payload = new { } });

        Assert.Equal("r-1", (string?)reply["id"]);
        Assert.True((bool?)reply["ok"]);
        Assert.Equal("none", (string?)reply["result"]!["state"]);
        Assert.Null(reply["result"]!["version"]);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("[]")]
    [InlineData("{\"type\":\"updateState\",\"payload\":{}}")]
    [InlineData("{\"id\":\"\",\"type\":\"updateState\"}")]
    [InlineData("{\"id\":7,\"type\":\"updateState\"}")]
    public async Task DropsMessagesThatNoReplyCouldReach(string message) =>
        Assert.Null(await _bridge.HandleAsync(message, TestContext.Current.CancellationToken));

    [Fact]
    public async Task DropsIdsLongerThanTheLimit()
    {
        var id = new string('i', BridgeRequest.MaxIdLength + 1);

        var reply = await _bridge.HandleAsync(
            JsonSerializer.Serialize(new { id, type = "updateState" }),
            TestContext.Current.CancellationToken);

        Assert.Null(reply);
    }

    [Fact]
    public async Task RefusesMessagesWithoutATypeOrAnObjectPayload()
    {
        var withoutType = await SendAsync(new { id = "a", payload = new { } });
        var textPayload = await SendAsync(new { id = "b", type = "updateState", payload = "x" });

        AssertFailure(withoutType, BridgeErrors.InvalidMessage);
        AssertFailure(textPayload, BridgeErrors.InvalidMessage);
    }

    [Fact]
    public async Task RefusesUnknownTypes()
    {
        var reply = await SendAsync(new { id = "a", type = "readFile", payload = new { } });

        AssertFailure(reply, BridgeErrors.UnknownType);
    }

    [Fact]
    public async Task OpensWebLinksInTheSystemBrowser()
    {
        var reply = await SendAsync(Open("https://www.leagueoflegends.com/"));

        Assert.True((bool?)reply["ok"]);
        Assert.Equal(new Uri("https://www.leagueoflegends.com/"), Assert.Single(_browser.Opened));
    }

    [Theory]
    [InlineData("javascript:alert(document.cookie)")]
    [InlineData("file:///etc/hosts")]
    [InlineData("http://127.0.0.1:4000/")]
    public async Task OpensNothingElse(string url)
    {
        var reply = await SendAsync(Open(url));

        AssertFailure(reply, BridgeErrors.InvalidUrl);
        Assert.Empty(_browser.Opened);
    }

    [Fact]
    public async Task RefusesAUrlThatIsNotAString()
    {
        var reply = await SendAsync(
            new { id = "a", type = "openExternal", payload = new { url = 1 } });

        AssertFailure(reply, BridgeErrors.InvalidUrl);
    }

    [Fact]
    public async Task ReportsABrowserThatDoesNotStart()
    {
        _browser.IsAvailable = false;

        var reply = await SendAsync(Open("https://example.com/"));

        AssertFailure(reply, BridgeErrors.BrowserFailed);
    }

    [Fact]
    public async Task SavesTheFileWhereTheUserChoseWithoutTellingThePath()
    {
        _shell.PickedPath = _folder.Combine("chosen.json");

        var reply = await SendAsync(Save("build.json", "application/json", JsonBase64));

        Assert.True((bool?)reply["ok"]);
        Assert.Equal("{\"saved\":true}", reply["result"]!.ToJsonString());
        Assert.Equal(
            "{\"a\":1}",
            await File.ReadAllTextAsync(_shell.PickedPath, TestContext.Current.CancellationToken));
        Assert.Equal(["build.json"], _shell.SuggestedNames);
    }

    [Fact]
    public async Task ReportsACancelledDialog()
    {
        var reply = await SendAsync(Save("build.json", "application/json", JsonBase64));

        Assert.True((bool?)reply["ok"]);
        Assert.False((bool?)reply["result"]!["saved"]);
    }

    [Theory]
    [InlineData("../build.json", "application/json", JsonBase64, BridgeErrors.InvalidName)]
    [InlineData("C:\\build.json", "application/json", JsonBase64, BridgeErrors.InvalidName)]
    [InlineData(null, "application/json", JsonBase64, BridgeErrors.InvalidName)]
    [InlineData("build.json", "text/plain; charset=utf-8", JsonBase64, BridgeErrors.InvalidMime)]
    [InlineData("build.json", null, JsonBase64, BridgeErrors.InvalidMime)]
    [InlineData("build.json", "application/json", "not base64!", BridgeErrors.InvalidContent)]
    [InlineData("build.json", "application/json", null, BridgeErrors.InvalidContent)]
    public async Task RefusesInvalidFilesBeforeAnyDialog(
        string? name,
        string? mime,
        string? base64,
        string error)
    {
        var reply = await SendAsync(Save(name, mime, base64));

        AssertFailure(reply, error);
        Assert.Empty(_shell.SuggestedNames);
    }

    [Fact]
    public async Task RefusesFilesOverTheLimit()
    {
        var base64 = new string('A', FileContent.MaxEncodedLength + 4);

        var reply = await SendAsync(Save("big.bin", "application/octet-stream", base64));

        AssertFailure(reply, BridgeErrors.TooLarge);
    }

    [Fact]
    public async Task ReportsAFileThatCannotBeWritten()
    {
        // A folder where the file should go: no system writes a file over it.
        _shell.PickedPath = _folder.Path;

        var reply = await SendAsync(Save("build.json", "application/json", JsonBase64));

        AssertFailure(reply, BridgeErrors.WriteFailed);
    }

    [Fact]
    public async Task RefusesToApplyAnUpdateThatIsNotReady()
    {
        var reply = await SendAsync(new { id = "a", type = "applyUpdate" });

        AssertFailure(reply, BridgeErrors.NoUpdate);
    }

    private static object Open(string url) =>
        new { id = "open", type = "openExternal", payload = new { url } };

    private static object Save(string? name, string? mime, string? base64) =>
        new { id = "save", type = "saveFile", payload = new { name, mime, base64 } };

    private static void AssertFailure(JsonObject reply, string error)
    {
        Assert.False((bool?)reply["ok"]);
        Assert.Equal(error, (string?)reply["error"]);
        Assert.Null(reply["result"]);
    }

    private async Task<JsonObject> SendAsync(object message)
    {
        var reply = await _bridge.HandleAsync(
            JsonSerializer.Serialize(message),
            TestContext.Current.CancellationToken);
        return Assert.IsType<JsonObject>(JsonNode.Parse(Assert.IsType<string>(reply)));
    }
}
