namespace LoDb.Domain.Derived.Champions;

/// <summary>
/// Number of a skin's art files.
/// </summary>
/// <remarks>
/// Data Dragon omits <c>num</c> before about 3.13.24 (UP 3); the skins are then listed in art
/// order, so the position in the list stands in for it.
/// </remarks>
public static class SkinNumber
{
    public static int Of(int? num, int index) => num ?? index;
}
