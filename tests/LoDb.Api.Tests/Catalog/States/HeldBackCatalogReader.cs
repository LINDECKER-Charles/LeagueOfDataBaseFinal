using System.Collections.Concurrent;
using LoDb.Domain.Languages;
using LoDb.Domain.Versions;
using LoDb.Ingestion.Catalog;
using LoDb.Ingestion.Catalog.Reading;

namespace LoDb.Api.Tests.Catalog.States;

/// <summary>
/// The API's catalog reader, but for the versions a test holds back: their catalogs answer as
/// one not stored yet does, whatever the store holds, as when the cold gate refuses a load.
/// </summary>
internal sealed class HeldBackCatalogReader(ICatalogReader inner) : ICatalogReader
{
    // Listed and recorded, never read by the tests of the group: a stored-only read of it is
    // pending.
    private static readonly PatchVersion NeverStored = PatchVersion.Parse("3.6.14");

    private readonly ConcurrentDictionary<PatchVersion, bool> heldBack = new();

    /// <summary>Holds the catalogs of <paramref name="version"/> back until released.</summary>
    public void HoldBack(PatchVersion version) => heldBack[version] = true;

    public void ReleaseAll() => heldBack.Clear();

    public Task<CatalogVersions> GetVersionsAsync(CancellationToken cancellationToken) =>
        inner.GetVersionsAsync(cancellationToken);

    public Task<PatchVersion?> GetLatestAsync(CancellationToken cancellationToken) =>
        inner.GetLatestAsync(cancellationToken);

    public Task<DateTimeOffset?> GetLatestPromotedAtAsync(CancellationToken cancellationToken) =>
        inner.GetLatestPromotedAtAsync(cancellationToken);

    public Task<IReadOnlyList<DdragonLanguage>> GetLanguagesAsync(
        CancellationToken cancellationToken) =>
        inner.GetLanguagesAsync(cancellationToken);

    public Task<CatalogLoad> GetAsync(
        PatchVersion version,
        DdragonLanguage language,
        ColdDemand demand,
        CancellationToken cancellationToken) =>
        heldBack.ContainsKey(version)
            ? inner.GetAsync(NeverStored, language, ColdDemand.StoredOnly, cancellationToken)
            : inner.GetAsync(version, language, demand, cancellationToken);
}
