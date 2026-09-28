using LoDb.Api.Modules.Accounts.Registration;

namespace LoDb.Api.Tests.Accounts.Units;

/// <summary>
/// The username of a new Google account, as the legacy allocator derives it: the first hint
/// that slugs into a valid name, then the first free suffix from 2 to 50, then random digits.
/// </summary>
public sealed class UsernameAllocatorTests
{
    private const int MaxLength = 24;
    private const int LastSequentialSuffix = 50;
    private const int RandomDigits = 6;

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData("Élodie", "Elodie")]
    [InlineData("new.player", "new-player")]
    [InlineData("  Jean   Pierre ", "Jean-Pierre")]
    [InlineData("__Zoë__", "Zoe")]
    [InlineData("o'neil_42", "o-neil-42")]
    [InlineData("Maximilian-Alexander-Friedrich", "Maximilian-Alexander-Fri")]
    public void HintIsSluggedIntoAUsername(string hint, string expected) =>
        Assert.Equal(expected, UsernameAllocator.Normalize(hint));

    [Theory]
    [InlineData(null)]
    [InlineData("  ")]
    [InlineData("Al")]
    [InlineData("__x__")]
    [InlineData("李小龍")]
    public void HintTooShortOnceSluggedIsSkipped(string? hint) =>
        Assert.Null(UsernameAllocator.Normalize(hint));

    [Fact]
    public async Task FirstUsableHintIsTaken()
    {
        var name = await AllocateAsync([null, "李小龍", "new.player"], []);

        Assert.Equal("new-player", name);
    }

    [Fact]
    public async Task WithoutUsableHintTheAccountIsASummoner()
    {
        var name = await AllocateAsync(["李小龍", "__"], []);

        Assert.Equal(UsernameAllocator.Fallback, name);
    }

    [Fact]
    public async Task TakenNameGetsTheFirstFreeSuffixWhateverTheCase()
    {
        var name = await AllocateAsync(["Élodie"], ["elodie", "ELODIE2"]);

        Assert.Equal("Elodie3", name);
    }

    [Fact]
    public async Task SuffixedNameStillFitsTheColumn()
    {
        var longest = new string('a', MaxLength);

        var name = await AllocateAsync([longest], [longest]);

        Assert.Equal(new string('a', MaxLength - 1) + "2", name);
    }

    [Fact]
    public async Task BeyondFiftyTheSuffixIsRandom()
    {
        var taken = Enumerable.Range(2, LastSequentialSuffix - 1)
            .Select(static suffix => $"Elodie{suffix}")
            .Append("Elodie");

        var name = await AllocateAsync(["Elodie"], taken);

        Assert.Matches($"^Elodie[1-9][0-9]{{{RandomDigits - 1}}}$", name);
    }

    private static Task<string> AllocateAsync(string?[] hints, IEnumerable<string> taken)
    {
        var names = taken.ToHashSet(StringComparer.OrdinalIgnoreCase);
        return UsernameAllocator.AllocateAsync(
            hints,
            (name, _) => Task.FromResult(names.Contains(name)),
            Cancellation);
    }
}
