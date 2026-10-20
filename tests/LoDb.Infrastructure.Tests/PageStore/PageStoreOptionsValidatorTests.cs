using LoDb.Infrastructure.PageStore;

namespace LoDb.Infrastructure.Tests.PageStore;

/// <summary>
/// The store's settings: unset, the store stays dormant and the host starts whatever its region
/// and class say; set, they must reach a bare https endpoint (plain http in development only),
/// a valid bucket and the One Zone class (another class in development only).
/// </summary>
public sealed class PageStoreOptionsValidatorTests
{
    [Fact]
    public void EmptySettingsLeaveTheStoreDormant()
    {
        var options = new PageStoreOptions();

        var result = new PageStoreOptionsValidator().Validate(name: null, options);

        Assert.True(result.Succeeded);
        Assert.False(options.IsConfigured);
    }

    // An env file line with no value binds an empty string.
    [Fact]
    public void ADormantStoreIgnoresAnEmptyRegionAndClass()
    {
        var options = new PageStoreOptions
        {
            Region = string.Empty,
            OneZoneStorageClass = string.Empty,
        };

        Assert.True(new PageStoreOptionsValidator().Validate(null, options).Succeeded);
    }

    [Theory]
    [InlineData("https://s3.fr-par.scw.cloud", false, true)]
    [InlineData("http://lodb-pages-store:9000", true, true)]
    [InlineData("http://s3.fr-par.scw.cloud", false, false)]
    [InlineData("https://s3.fr-par.scw.cloud/lodb-pages-prod", false, false)]
    [InlineData("https://s3.fr-par.scw.cloud/?x=1", false, false)]
    [InlineData("s3.fr-par.scw.cloud", false, false)]
    public void TheEndpointIsABareBaseUrlOverHttps(
        string endpoint,
        bool allowInsecure,
        bool isValid)
    {
        var options = new PageStoreOptions
        {
            Endpoint = endpoint,
            AllowInsecureEndpoint = allowInsecure,
        };

        Assert.Equal(isValid, new PageStoreOptionsValidator().Validate(null, options).Succeeded);
    }

    // A bucket S3 rejects, a region with a scheme or empty, a class neither Scaleway nor MinIO
    // knows.
    [Theory]
    [InlineData("Lodb_Pages", "fr-par", "ONEZONE_IA")]
    [InlineData("lodb-pages-prod", "https://fr-par", "ONEZONE_IA")]
    [InlineData("lodb-pages-prod", "", "ONEZONE_IA")]
    [InlineData("lodb-pages-prod", "fr-par", "GLACIER")]
    public void ANameS3WouldRejectIsRefused(string bucket, string region, string storageClass)
    {
        var options = Configured(isInsecure: false);
        options.Bucket = bucket;
        options.Region = region;
        options.OneZoneStorageClass = storageClass;

        Assert.False(new PageStoreOptionsValidator().Validate(null, options).Succeeded);
    }

    // STANDARD or REDUCED_REDUNDANCY on a host would silently double the storage bill.
    [Theory]
    [InlineData("ONEZONE_IA", false, true)]
    [InlineData("STANDARD", false, false)]
    [InlineData("REDUCED_REDUNDANCY", false, false)]
    [InlineData("REDUCED_REDUNDANCY", true, true)]
    [InlineData("STANDARD", true, true)]
    public void OnlyTheDevelopmentStoreTakesAnotherClass(
        string storageClass,
        bool isInsecure,
        bool isValid)
    {
        var options = Configured(isInsecure);
        options.OneZoneStorageClass = storageClass;

        Assert.Equal(isValid, new PageStoreOptionsValidator().Validate(null, options).Succeeded);
    }

    private static PageStoreOptions Configured(bool isInsecure) => new()
    {
        Endpoint = isInsecure ? "http://lodb-pages-store:9000" : "https://s3.fr-par.scw.cloud",
        AllowInsecureEndpoint = isInsecure,
        Bucket = "lodb-pages-prod",
        AccessKey = "SCWFAKEACCESSKEY0000",
        SecretKey = "fake-secret",
    };
}
