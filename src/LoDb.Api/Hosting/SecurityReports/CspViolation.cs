namespace LoDb.Api.Hosting.SecurityReports;

/// <summary>
/// What the API keeps of one violation report: the directive broken and whether the browser
/// blocked it or only reported it. Never the page, the resource or the sample: they may name
/// a visitor (a profile, a share token) and say nothing more about the breach.
/// </summary>
/// <param name="Directive">A name of <see cref="CspDirectives"/>, lower-case.</param>
/// <param name="Disposition">A value of <see cref="CspDispositions"/>.</param>
internal sealed record CspViolation(string Directive, string Disposition);
