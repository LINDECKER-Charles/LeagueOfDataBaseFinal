using System.ComponentModel.DataAnnotations;

namespace LoDb.Api.Hosting.SecurityReports;

/// <summary>
/// Bounds of the browsers' Content-Security-Policy reports, bound from
/// <c>LoDb:SecurityReports</c>.
/// </summary>
internal sealed class SecurityReportsOptions
{
    public const string SectionName = "LoDb:SecurityReports";

    /// <summary>
    /// A report weighs about a kilobyte; a browser may batch a few dozen of them in one body.
    /// </summary>
    public const int DefaultMaxBodyBytes = 64 * 1024;

    /// <summary>A page that breaks its policy sends a few reports per load, not more.</summary>
    public const int DefaultPerClientPerMinute = 30;

    /// <summary>What every visitor together may send, whatever the number of addresses.</summary>
    public const int DefaultPerMinute = 600;

    private const int AtLeastOne = 1;

    /// <summary>Largest body read, in bytes; a larger one is answered 413 and counted.</summary>
    [Range(AtLeastOne, HostingOptions.DefaultMaxRequestBodySize)]
    public int MaxBodyBytes { get; set; } = DefaultMaxBodyBytes;

    /// <summary>Report bodies one client address may send in a minute.</summary>
    [Range(AtLeastOne, int.MaxValue)]
    public int PerClientPerMinute { get; set; } = DefaultPerClientPerMinute;

    /// <summary>Report bodies the API reads in a minute, from every client together.</summary>
    [Range(AtLeastOne, int.MaxValue)]
    public int PerMinute { get; set; } = DefaultPerMinute;
}
