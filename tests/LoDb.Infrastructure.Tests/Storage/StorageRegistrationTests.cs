using LoDb.Infrastructure.Storage;
using LoDb.Infrastructure.Storage.Blobs;
using LoDb.Infrastructure.Storage.Datasets;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace LoDb.Infrastructure.Tests.Storage;

/// <summary>
/// The root is checked when the host starts or a store is first resolved, never while the
/// services are registered.
/// </summary>
public sealed class StorageRegistrationTests
{
    private const string RelativeRoot = "relative/storage";

    // The rule is "not blank and fully qualified": one blank and one relative root.
    [Theory]
    [InlineData("")]
    [InlineData(RelativeRoot)]
    public void AStoreIsNotResolvedWithoutAnAbsoluteRoot(string? root)
    {
        using var services = TemporaryStorage.Build(root);

        Assert.Throws<OptionsValidationException>(services.GetRequiredService<IBlobStore>);
        Assert.Throws<OptionsValidationException>(services.GetRequiredService<IDatasetStore>);
    }

    [Fact]
    public async Task TheHostDoesNotStartWithoutAnAbsoluteRoot()
    {
        var builder = Host.CreateEmptyApplicationBuilder(new HostApplicationBuilderSettings());
        builder.Configuration[TemporaryStorage.RootKey] = RelativeRoot;
        builder.Services.AddLoDbStorage(builder.Configuration);
        using var host = builder.Build();

        await Assert.ThrowsAsync<OptionsValidationException>(
            () => host.StartAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public void TheStoresAreSingletons()
    {
        using var storage = new TemporaryStorage();

        Assert.Same(storage.Blobs, storage.Blobs);
        Assert.Same(storage.Datasets, storage.Datasets);
    }
}
