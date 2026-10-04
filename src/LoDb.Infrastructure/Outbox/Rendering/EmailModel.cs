using System.Text.Json;

namespace LoDb.Infrastructure.Outbox.Rendering;

/// <summary>
/// The stored model of a message (a JSON object of strings), read by key; see
/// <see cref="EmailModelKeys"/>.
/// </summary>
internal sealed class EmailModel
{
    private readonly IReadOnlyDictionary<string, string?> _values;

    private EmailModel(IReadOnlyDictionary<string, string?> values) => _values = values;

    public static EmailModel Parse(string json)
    {
        try
        {
            return new EmailModel(
                JsonSerializer.Deserialize<Dictionary<string, string?>>(json)
                ?? throw new EmailModelException("The model is null."));
        }
        catch (JsonException)
        {
            throw new EmailModelException("The model is not a JSON object of strings.");
        }
    }

    /// <summary>The value of <paramref name="key"/>, or null when missing or blank.</summary>
    public string? Optional(string key) =>
        _values.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value
            : null;

    public string Required(string key) =>
        Optional(key) ?? throw new EmailModelException($"The model has no value for {key}.");
}
