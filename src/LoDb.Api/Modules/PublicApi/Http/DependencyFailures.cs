using System.Data.Common;
using System.Net.Sockets;

namespace LoDb.Api.Modules.PublicApi.Http;

/// <summary>
/// Tells an outage of the database or of the storage, which go-api answers with a 503, from
/// a fault of the code, which it answers with a 500.
/// </summary>
internal static class DependencyFailures
{
    public static bool IsOutage(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is DbException
                or TimeoutException
                or IOException
                or SocketException
                or UnauthorizedAccessException
                or DependencyUnavailableException)
            {
                return true;
            }

            if (current is AggregateException aggregate
                && aggregate.InnerExceptions.Any(IsOutage))
            {
                return true;
            }
        }

        return false;
    }
}
