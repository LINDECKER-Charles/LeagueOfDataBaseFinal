using LoDb.Api.Modules.Admin.Prerender.Demand;
using LoDb.Api.Modules.Admin.Prerender.Demand.Validation;
using LoDb.Domain.Catalog;
using LoDb.Domain.Languages;

namespace LoDb.Api.Tests.Prerender.Demand;

/// <summary>
/// The queue's settings are checked when the host starts; on by default, with half the pass
/// SSR's renders while a pass runs. A queued path reads back into its parts.
/// </summary>
public sealed class DemandSettingsTests
{
    public static TheoryData<string, string> InvalidSettings => new()
    {
        { "RefusedPagesUrl", "ftp://lodb-web-ssr:9464/refused" },
        { "RefusedPagesUrl", "http://lodb-web-ssr:9464/refused?after=1" },
        { "QueueSharePercent", "0" },
        { "QueueSharePercent", "101" },
        { "RetentionDays", "0" },
        { "MaxPendingPages", "99" },
        { "MaxAttempts", "0" },
        { "MaxAttempts", "11" },
    };

    [Fact]
    public void DefaultsSwitchTheQueueOnWithHalfTheRendersBesideAPass()
    {
        var options = new DemandOptions();

        Assert.True(options.Enabled);
        Assert.Equal((50, 30, 100_000, 3), (options.QueueSharePercent, options.RetentionDays,
            options.MaxPendingPages, options.MaxAttempts));
        Assert.True(new DemandOptionsValidator().Validate(null, options).Succeeded);
    }

    [Theory]
    [MemberData(nameof(InvalidSettings))]
    public void InvalidSettingIsRefused(string key, string value)
    {
        var options = new DemandOptions();
        typeof(DemandOptions).GetProperty(key)!.SetValue(
            options,
            key == nameof(DemandOptions.RefusedPagesUrl) ? value : int.Parse(value, null));

        Assert.True(new DemandOptionsValidator().Validate(null, options).Failed);
    }

    [Fact]
    public void QueuedPathReadsBackIntoItsParts()
    {
        var page = DemandPath.Parse("/zh-hans/16.11.1/items/1036-long-sword");

        Assert.NotNull(page);
        Assert.Equal(
            (UiLocale.ZhHans, "16.11.1", ResourceType.Items, "items/1036-long-sword", "zh-hans"),
            (page.Locale, page.Version.Value, page.Type, page.PagePath, page.LocaleCode));
    }

    [Theory]
    [InlineData("/fr/16.11.1")]
    [InlineData("/fr/16.11.1/items/1036/x")]
    [InlineData("/fr/16.11.1/items/")]
    [InlineData("/fr/latest/items")]
    public void OtherShapesAreNoPath(string path)
    {
        Assert.Null(DemandPath.Parse(path));
    }
}
