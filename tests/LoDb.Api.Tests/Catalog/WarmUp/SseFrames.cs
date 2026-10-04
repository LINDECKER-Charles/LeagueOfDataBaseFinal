using System.Text.Json;

namespace LoDb.Api.Tests.Catalog.WarmUp;

/// <summary>The JSON data of each event of a Server-Sent Events body, in order.</summary>
internal static class SseFrames
{
    private const string EventSeparator = "\n\n";
    private const string DataField = "data: ";

    public static IReadOnlyList<JsonElement> Parse(string body) =>
    [
        .. body.Split(EventSeparator, StringSplitOptions.RemoveEmptyEntries)
            .SelectMany(static frame => frame.Split('\n'))
            .Where(static line => line.StartsWith(DataField, StringComparison.Ordinal))
            .Select(static line =>
                JsonDocument.Parse(line[DataField.Length..]).RootElement.Clone()),
    ];
}
