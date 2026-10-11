using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace LoDb.Api.Tests.Hosting.Support;

/// <summary>
/// The check of the options that stops the host when it starts, run on what one registration
/// adds for some settings, without building a host: a bound out of range is refused as the
/// start would refuse it, at the cost of a service collection.
/// </summary>
internal static class StartupValidation
{
    /// <summary>
    /// The refusal of the options <typeparamref name="TOptions"/>, the only ones refused once
    /// <paramref name="register"/> has added its services for <paramref name="settings"/>.
    /// </summary>
    public static OptionsValidationException RefusalOf<TOptions>(
        Action<WebApplicationBuilder> register,
        IReadOnlyDictionary<string, string?> settings)
    {
        var refusal = Assert.Throws<OptionsValidationException>(() => Run(register, settings));
        Assert.Equal(typeof(TOptions), refusal.OptionsType);
        return refusal;
    }

    /// <summary>
    /// Runs the validators the host runs at start on what <paramref name="register"/> adds to
    /// a builder configured with <paramref name="settings"/>.
    /// </summary>
    /// <exception cref="OptionsValidationException">Options were refused.</exception>
    public static void Run(
        Action<WebApplicationBuilder> register,
        IReadOnlyDictionary<string, string?> settings)
    {
        ArgumentNullException.ThrowIfNull(register);
        var builder = WebApplication.CreateEmptyBuilder(new WebApplicationOptions());
        builder.Configuration.AddInMemoryCollection(settings);
        register(builder);
        using var services = builder.Services.BuildServiceProvider();
        services.GetRequiredService<IStartupValidator>().Validate();
    }
}
