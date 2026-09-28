namespace LoDb.Api.Modules.Admin.Http;

/// <summary>The pages of the admin lists: 25 rows each, as in the legacy admin.</summary>
internal static class AdminPaging
{
    public const int PageSize = 25;

    // Far past any list; keeps the offset of a forged page number within an int.
    private const int LastPage = 100_000;

    /// <summary>The page asked for, from 1; the first one when absent or out of range.</summary>
    public static int PageOf(int? page) => Math.Clamp(page ?? 1, 1, LastPage);

    /// <summary>Rows before <paramref name="page"/>.</summary>
    public static int Skip(int page) => (page - 1) * PageSize;

    /// <summary>Pages of <paramref name="total"/> rows, at least one.</summary>
    public static int Pages(int total) => Math.Max(1, (total + PageSize - 1) / PageSize);
}
