using System.Net;

namespace LoDb.Infrastructure.Analytics.Geo;

/// <summary>The country of a client address, resolved locally.</summary>
internal interface IGeoLocator
{
    /// <returns>
    /// Null for a private or unknown address, and for every address without a database.
    /// </returns>
    GeoCountry? Locate(IPAddress? address);
}
