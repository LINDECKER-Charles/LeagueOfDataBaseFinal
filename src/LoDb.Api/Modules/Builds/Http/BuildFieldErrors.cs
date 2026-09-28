namespace LoDb.Api.Modules.Builds.Http;

/// <summary>
/// Codes of the invalid fields of a build, collected before anything is saved and answered
/// together as one validation problem.
/// </summary>
internal sealed class BuildFieldErrors
{
    private readonly Dictionary<string, List<string>> _codes = new(StringComparer.Ordinal);

    public bool IsEmpty => _codes.Count == 0;

    public void Add(string field, string code) => Add(field, [code]);

    public void Add(string field, IEnumerable<string> codes)
    {
        ArgumentNullException.ThrowIfNull(codes);
        if (!_codes.TryGetValue(field, out var known))
        {
            known = [];
            _codes[field] = known;
        }

        known.AddRange(codes);
        if (known.Count == 0)
        {
            _codes.Remove(field);
        }
    }

    public BuildProblem ToProblem(IReadOnlyList<string>? unavailableItems = null) =>
        BuildProblem.Validation(
            _codes.ToDictionary(
                static field => field.Key,
                static field => field.Value.ToArray(),
                StringComparer.Ordinal),
            unavailableItems ?? []);
}
