using LoDb.Api.Modules.Accounts.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace LoDb.Api.Tests.Accounts.Units;

/// <summary>The links of the verification and reset e-mails last one hour.</summary>
public sealed class EmailTokenLifetimeTests
{
    [Fact]
    public void LinksLastOneHour()
    {
        var services = new ServiceCollection().AddLogging();
        services.AddAccountsAuthentication();
        using var provider = services.BuildServiceProvider();

        var options = provider.GetRequiredService<IOptions<DataProtectionTokenProviderOptions>>();

        Assert.Equal(TimeSpan.FromHours(1), options.Value.TokenLifespan);
    }
}
