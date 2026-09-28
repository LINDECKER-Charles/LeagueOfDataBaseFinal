using System.Text.Json;
using LoDb.Domain.Builds.Structures;
using LoDb.Infrastructure.Persistence.Builds;

namespace LoDb.Api.Modules.Builds.Storage;

/// <summary>
/// The structure of a stored build: its champion column and its two JSON columns, read as
/// the rules read a submission and written in the legacy shape.
/// </summary>
/// <remarks>
/// The columns keep the keys the legacy stack wrote (<c>primaryStyleId</c>,
/// <c>label</c>…), so that both stacks read the same rows.
/// </remarks>
internal static class StoredStructures
{
    private const string PrimaryStyleKey = "primaryStyleId";
    private const string PrimarySelectionsKey = "primarySelections";
    private const string SecondaryStyleKey = "secondaryStyleId";
    private const string SecondarySelectionsKey = "secondarySelections";
    private const string LabelKey = "label";
    private const string NoteKey = "note";
    private const string ItemsKey = "items";

    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    /// <summary>The stored structure as submitted, for the import to project it.</summary>
    public static BuildStructureInput Read(Build build)
    {
        ArgumentNullException.ThrowIfNull(build);
        return new BuildStructureInput
        {
            ChampionId = build.ChampionId,
            Runes = Parse(build.Runes, ReadRunes),
            Steps = Parse(build.Steps, ReadSteps),
        };
    }

    /// <summary>The stored structure in its canonical shape, for display and editing.</summary>
    public static BuildStructure Normalized(Build build) =>
        BuildStructureNormalizer.Normalize(Read(build));

    public static void Write(Build build, BuildStructure structure)
    {
        ArgumentNullException.ThrowIfNull(build);
        ArgumentNullException.ThrowIfNull(structure);
        build.ChampionId = structure.ChampionId;
        build.Runes = JsonSerializer.Serialize(structure.Runes, Options);
        build.Steps = JsonSerializer.Serialize(structure.Steps, Options);
    }

    // The columns are jsonb, so always JSON; a blank one only comes from a hand-written row.
    private static TValue? Parse<TValue>(string? json, Func<JsonElement, TValue?> read)
        where TValue : class
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            return read(document.RootElement);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static RunePageInput? ReadRunes(JsonElement runes) =>
        StoredJson.IsRecord(runes)
            ? new RunePageInput
            {
                PrimaryStyleId = StoredJson.Integer(StoredJson.Field(runes, PrimaryStyleKey)),
                PrimarySelections = Integers(StoredJson.Field(runes, PrimarySelectionsKey)),
                SecondaryStyleId = StoredJson.Integer(StoredJson.Field(runes, SecondaryStyleKey)),
                SecondarySelections = Integers(StoredJson.Field(runes, SecondarySelectionsKey)),
            }
            : null;

    private static IReadOnlyList<int?>? Integers(JsonElement? list) =>
        StoredJson.List(list, static id => StoredJson.Integer(id));

    private static IReadOnlyList<StepInput?>? ReadSteps(JsonElement steps) =>
        StoredJson.List(steps, ReadStep);

    private static StepInput? ReadStep(JsonElement step)
    {
        if (!StoredJson.IsRecord(step))
        {
            return null;
        }

        var note = StoredJson.Field(step, NoteKey);
        return new StepInput
        {
            Label = StoredJson.Scalar(StoredJson.Field(step, LabelKey)),
            Note = note is { ValueKind: JsonValueKind.String } text ? text.GetString() : null,
            IsNoteMalformed =
                note is { ValueKind: not (JsonValueKind.Null or JsonValueKind.String) },
            Items = StoredJson.List(
                StoredJson.Field(step, ItemsKey),
                static id => StoredJson.Scalar(id)),
        };
    }
}
