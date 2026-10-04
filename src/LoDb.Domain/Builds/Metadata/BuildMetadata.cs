using LoDb.Domain.Builds.Rules;

namespace LoDb.Domain.Builds.Metadata;

/// <summary>
/// The texts around a structure: a name of 3 to 80 characters and an optional description of
/// 2000 at most, both trimmed before they are counted.
/// </summary>
public static class BuildMetadata
{
    /// <summary>The name as stored: trimmed, empty when none was sent.</summary>
    public static string Name(string? submitted) => submitted?.Trim() ?? string.Empty;

    /// <summary>The description as stored: trimmed, null when blank.</summary>
    public static string? Description(string? submitted)
    {
        var trimmed = submitted?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }

    /// <param name="name">A name as <see cref="Name"/> returns it.</param>
    public static bool IsNameValid(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        return TextLength.IsBetween(name, BuildLimits.NameMin, BuildLimits.NameMax);
    }

    /// <param name="description">A description as <see cref="Description"/> returns it.</param>
    public static bool IsDescriptionValid(string? description) =>
        description is null || TextLength.IsWithin(description, BuildLimits.DescriptionMax);
}
