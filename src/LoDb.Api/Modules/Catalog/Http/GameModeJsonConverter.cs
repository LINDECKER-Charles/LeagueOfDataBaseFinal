using System.Text.Json;
using System.Text.Json.Serialization;
using LoDb.Domain.Catalog.Modes;

namespace LoDb.Api.Modules.Catalog.Http;

/// <summary>
/// Writes a game mode as its persisted code ("sr", "nexus_blitz"), the value the builds store
/// and the <c>mode</c> query parameter takes; no naming policy derives it from the name.
/// </summary>
internal sealed class GameModeJsonConverter : JsonConverter<GameMode>
{
    public override GameMode Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options) =>
        GameModes.TryParse(reader.GetString(), out var mode)
            ? mode
            : throw new JsonException("Unknown game mode code.");

    public override void Write(
        Utf8JsonWriter writer,
        GameMode value,
        JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        writer.WriteStringValue(GameModes.Code(value));
    }
}
