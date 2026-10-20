using LoDb.Api.Modules.Admin.Scaleway;
using LoDb.Api.Tests.Admin.Scaleway.Support;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace LoDb.Api.Tests.Admin.Scaleway;

/// <summary>
/// Unset, the bill is off without a word; once its key is set, the three ids are required and
/// checked when the host starts, as is any id set without it.
/// </summary>
public sealed class ScalewayBillSettingsTests
{
    private const string Section = "LoDb:ScalewayBilling:";

    [Fact]
    public void EmptySettingsAreValidAndLeaveTheBillOff()
    {
        var options = Options(new Dictionary<string, string?>());

        Assert.False(options.IsConfigured);
    }

    [Fact]
    public void KeyAndItsThreeIdsAreValid()
    {
        var options = Options(ScalewayHarness.Settings());

        Assert.True(options.IsConfigured);
    }

    [Fact]
    public void BlankKeyLeavesTheBillOffWithoutRequiringTheIds()
    {
        var options = Options(new Dictionary<string, string?>
        {
            [Section + "SecretKey"] = "  \t ",
        });

        Assert.False(options.IsConfigured);
    }

    [Fact]
    public void BlankIdWithoutTheKeyLeavesTheBillOff()
    {
        var options = Options(new Dictionary<string, string?>
        {
            [Section + "OrganizationId"] = "  \t ",
        });

        Assert.False(options.IsConfigured);
    }

    [Theory]
    [InlineData("OrganizationId")]
    [InlineData("BackupsProjectId")]
    [InlineData("PagesProjectId")]
    public void KeyWithoutAnIdIsRefusedAtStartup(string key)
    {
        var settings = ScalewayHarness.Settings();
        settings[Section + key] = string.Empty;

        var refusal = Assert.Throws<OptionsValidationException>(() => Options(settings));

        Assert.Contains(key, refusal.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void MistypedIdIsRefusedEvenWithoutTheKey()
    {
        var settings = new Dictionary<string, string?>
        {
            [Section + "PagesProjectId"] = "lodb-pages-prod",
        };

        Assert.Throws<OptionsValidationException>(() => Options(settings));
    }

    private static ScalewayBillingOptions Options(Dictionary<string, string?> settings)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        using var services = new ServiceCollection()
            .AddAdminScalewayBilling(configuration)
            .BuildServiceProvider();
        return services.GetRequiredService<IOptions<ScalewayBillingOptions>>().Value;
    }
}
