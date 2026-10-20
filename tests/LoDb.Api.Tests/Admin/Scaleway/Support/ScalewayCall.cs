using System.Globalization;
using Microsoft.AspNetCore.WebUtilities;

namespace LoDb.Api.Tests.Admin.Scaleway.Support;

/// <summary>A request the fake billing API received.</summary>
/// <param name="Url">The absolute URL asked for.</param>
/// <param name="Token">The <c>X-Auth-Token</c> it carried, if any.</param>
internal sealed record ScalewayCall(Uri Url, string? Token)
{
    /// <summary>The value of one query parameter, decoded; empty when absent.</summary>
    public string Query(string name) =>
        QueryHelpers.ParseQuery(Url.Query).TryGetValue(name, out var value)
            ? value.ToString()
            : string.Empty;

    public string Period => Query("billing_period");

    public int Page => int.Parse(Query("page"), CultureInfo.InvariantCulture);
}
