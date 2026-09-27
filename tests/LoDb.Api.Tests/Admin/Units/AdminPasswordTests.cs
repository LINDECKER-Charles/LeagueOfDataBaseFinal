using LoDb.Api.Cli.Admin;
using LoDb.Api.Modules.Accounts.Security.Policy;

namespace LoDb.Api.Tests.Admin.Units;

/// <summary>The password <c>admin create</c> draws for an account it creates.</summary>
public sealed class AdminPasswordTests
{
    private const int Draws = 200;

    [Fact]
    public void EveryDrawMeetsTheCnilPolicy()
    {
        var passwords = Enumerable.Range(0, Draws)
            .Select(static _ => AdminPassword.Generate())
            .ToList();

        Assert.All(passwords, static password =>
            Assert.True(CnilPasswordValidator.Validate(password).Succeeded, password));
        Assert.Equal(Draws, passwords.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void NoCharacterReadsLikeAnother()
    {
        var characters = string.Concat(
            Enumerable.Range(0, Draws).Select(static _ => AdminPassword.Generate()));

        Assert.DoesNotContain(characters, static c => c is 'l' or 'I' or 'O' or '0' or '1');
    }
}
