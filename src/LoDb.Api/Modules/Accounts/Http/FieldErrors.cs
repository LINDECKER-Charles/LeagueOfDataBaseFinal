using Microsoft.AspNetCore.Identity;

namespace LoDb.Api.Modules.Accounts.Http;

/// <summary>
/// Codes of the invalid fields of a request, collected before anything changes and answered
/// together as one validation problem.
/// </summary>
internal sealed class FieldErrors
{
    /// <summary>The field is missing or blank.</summary>
    public const string Required = "required";

    /// <summary>The field holds a value of a shape it can never take.</summary>
    public const string Malformed = "malformed";

    private readonly Dictionary<string, List<string>> _codes = new(StringComparer.Ordinal);

    public bool IsEmpty => _codes.Count == 0;

    public void Add(string field, string code)
    {
        if (!_codes.TryGetValue(field, out var codes))
        {
            codes = [];
            _codes[field] = codes;
        }

        codes.Add(code);
    }

    /// <summary>Adds the codes of Identity's errors, such as the password policy's.</summary>
    public void Add(string field, IEnumerable<IdentityError> errors)
    {
        foreach (var error in errors)
        {
            Add(field, error.Code);
        }
    }

    /// <summary>Adds <see cref="Required"/> to a blank field.</summary>
    public void RequireText(string field, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            Add(field, Required);
        }
    }

    /// <summary>
    /// Adds <see cref="Required"/> to a blank field, and <see cref="Malformed"/> to a value
    /// <paramref name="isWellFormed"/> refuses.
    /// </summary>
    public void RequireShape(string field, string? value, Func<string, bool> isWellFormed)
    {
        RequireText(field, value);
        CheckShape(field, value, isWellFormed);
    }

    /// <summary>
    /// Adds <see cref="Malformed"/> to a value <paramref name="isWellFormed"/> refuses; a
    /// blank field, left out, is not checked.
    /// </summary>
    public void CheckShape(string field, string? value, Func<string, bool> isWellFormed)
    {
        ArgumentNullException.ThrowIfNull(isWellFormed);
        if (!string.IsNullOrWhiteSpace(value) && !isWellFormed(value))
        {
            Add(field, Malformed);
        }
    }

    public AccountProblem ToProblem() => AccountProblem.Validation(
        _codes.ToDictionary(
            static field => field.Key,
            static field => field.Value.ToArray(),
            StringComparer.Ordinal));
}
