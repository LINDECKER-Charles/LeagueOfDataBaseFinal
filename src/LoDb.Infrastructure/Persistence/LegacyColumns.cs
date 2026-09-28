using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LoDb.Infrastructure.Persistence;

/// <summary>
/// Column defaults declared the way Doctrine declared them, so that the baseline creates
/// the same catalog.
/// </summary>
/// <remarks>
/// A default is a property of the column only: EF always sends the value it holds, so a
/// <c>false</c>, a <c>0</c> or a <c>null</c> set in code is never mistaken for "unset".
/// </remarks>
internal static class LegacyColumns
{
    /// <summary><c>DEFAULT NULL</c>, which PostgreSQL keeps on typmod columns only.</summary>
    public static PropertyBuilder<T> HasNullDefault<T>(this PropertyBuilder<T> property) =>
        property.HasDefaultValueSql("NULL").ValueGeneratedNever();

    public static PropertyBuilder<T> HasLegacyDefault<T>(
        this PropertyBuilder<T> property,
        T value) =>
        property.HasDefaultValue(value).ValueGeneratedNever();
}
