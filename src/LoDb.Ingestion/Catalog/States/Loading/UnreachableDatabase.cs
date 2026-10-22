using System.Data.Common;

namespace LoDb.Ingestion.Catalog.States.Loading;

/// <summary>
/// Tells a read of the index that the database failed it: the provider's exception, as is or
/// wrapped by EF (the <see cref="InvalidOperationException"/> of a transient failure).
/// </summary>
internal static class UnreachableDatabase
{
    public static bool IsCause(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is DbException or StateIndexUnavailableException)
            {
                return true;
            }
        }

        return false;
    }
}
