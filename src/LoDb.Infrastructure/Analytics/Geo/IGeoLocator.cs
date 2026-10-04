using System.Net;

namespace LoDb.Infrastructure.Analytics.Geo;

/// <summary>The country of a client address, resolved locally.</summary>
internal interface IGeoLocator
{
    /// <summary>
    /// Whether a database could be opened: without one, every view goes without a country
    /// and the admin says why its country breakdown is empty.
    /// </summary>
    bool IsAvailable { get; }

    /// <returns>
    /// Null for a private or unknown address, and for every address without a database.
    /// </returns>
    GeoCountry? Locate(IPAddress? address);
}
