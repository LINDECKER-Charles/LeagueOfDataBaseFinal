using LoDb.Api.Modules.Builds.Catalogs;
using LoDb.Api.Modules.Builds.Editing.Bodies;
using LoDb.Api.Modules.Builds.Http;
using LoDb.Api.Modules.Catalog.Http;
using LoDb.Api.Modules.Catalog.Reading;
using LoDb.Domain.Builds.Metadata;
using LoDb.Domain.Builds.Rules;
using LoDb.Domain.Builds.Structures;
using LoDb.Domain.Catalog.Modes;
using LoDb.Domain.Languages;
using LoDb.Domain.Versions;

namespace LoDb.Api.Modules.Builds.Editing;

/// <summary>
/// Checks a submitted build as the legacy editor did: its texts, its patch and language
/// against what Data Dragon lists, then its structure against the catalog of that patch and
/// the map of its mode. Every refusal of the fields comes in one answer.
/// </summary>
internal sealed class BuildSubmissions(BuildVersions versions, CatalogGateway gateway)
{
    /// <param name="request">The submitted build.</param>
    /// <param name="lang">
    /// The language of the editor, in which a refusal names the items; en_US when unset.
    /// </param>
    /// <param name="cancellationToken">Aborts the reads.</param>
    public async Task<SubmissionCheck> CheckAsync(
        BuildRequest request,
        string? lang,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var names = lang ?? DdragonLanguage.EnUs.Code;
        if (await PinAsync(request, names, cancellationToken) is not { } pin)
        {
            return SubmissionCheck.Refuse(CatalogProblem.UpstreamUnavailable());
        }

        // Without a structure or a listed patch, there is nothing to check a structure against.
        var errors = MetadataErrors(request, pin);
        if (request.Structure is null || !pin.IsVersionListed)
        {
            return SubmissionCheck.Refuse(errors.ToProblem());
        }

        var read = await gateway.ReadAsync(new CatalogScope(pin.Version, names), cancellationToken);
        if (!read.IsOpen)
        {
            return SubmissionCheck.Refuse(read.Problem);
        }

        var input = request.Structure.ToInput();
        var catalog = BuildCatalogs.Of(read.Context.Catalog);
        var verdict = BuildCatalogGate.Evaluate(input, pin.Mode, catalog);
        errors.Add(BuildFields.Structure, verdict.Codes);
        return errors.IsEmpty
            ? SubmissionCheck.Accept(Accepted(request, pin, input))
            : SubmissionCheck.Refuse(errors.ToProblem(verdict.UnavailableItems));
    }

    private static BuildFieldErrors MetadataErrors(BuildRequest request, Pin pin)
    {
        var errors = new BuildFieldErrors();
        if (!BuildMetadata.IsNameValid(BuildMetadata.Name(request.Name)))
        {
            errors.Add(BuildFields.Name, BuildErrors.NameLength);
        }

        if (!BuildMetadata.IsDescriptionValid(BuildMetadata.Description(request.Description)))
        {
            errors.Add(BuildFields.Description, BuildErrors.DescriptionLength);
        }

        if (request.Structure is null)
        {
            errors.Add(BuildFields.Structure, BuildErrors.StructureInvalid);
        }

        if (!pin.IsVersionListed)
        {
            errors.Add(BuildFields.GameVersion, BuildErrors.VersionUnknown);
        }

        if (!pin.IsLanguageKnown)
        {
            errors.Add(BuildFields.Language, BuildErrors.LanguageUnknown);
        }

        return errors;
    }

    private static BuildSubmission Accepted(
        BuildRequest request,
        Pin pin,
        BuildStructureInput input) =>
        new()
        {
            Name = BuildMetadata.Name(request.Name),
            Description = BuildMetadata.Description(request.Description),
            IsPublic = request.IsPublic,
            GameVersion = pin.Version,
            GameMode = pin.Mode,
            Language = pin.Language,
            Structure = BuildStructureNormalizer.Normalize(input),
        };

    private static string? Trimmed(string? text) =>
        string.IsNullOrWhiteSpace(text) ? null : text.Trim();

    // Null when Data Dragon could not be read: its lists alone tell a patch or a language.
    private async Task<Pin?> PinAsync(
        BuildRequest request,
        string names,
        CancellationToken cancellationToken)
    {
        var version = Trimmed(request.GameVersion)
            ?? (await versions.LatestAsync(cancellationToken))?.Value;
        var language = Trimmed(request.Language) ?? names;
        bool? isListed = PatchVersion.TryParse(version, out var patch)
            ? await versions.IsListedAsync(patch, cancellationToken)
            : false;
        bool? isKnown = DdragonLanguage.TryParse(language, out var parsed)
            ? await versions.IsKnownAsync(parsed, cancellationToken)
            : false;
        if (version is null || isListed is not { } listed || isKnown is not { } known)
        {
            return null;
        }

        return new Pin
        {
            Version = patch?.Value ?? version,
            IsVersionListed = listed,
            Language = language,
            IsLanguageKnown = known,
            Mode = request.GameMode ?? GameModes.Default,
        };
    }

    /// <summary>The patch, language and mode a submission names, and whether they exist.</summary>
    private sealed record Pin
    {
        public required string Version { get; init; }

        public required bool IsVersionListed { get; init; }

        public required string Language { get; init; }

        public required bool IsLanguageKnown { get; init; }

        public required GameMode Mode { get; init; }
    }
}
