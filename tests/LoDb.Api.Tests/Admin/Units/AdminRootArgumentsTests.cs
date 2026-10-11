using LoDb.Api.Cli.Admin;

namespace LoDb.Api.Tests.Admin.Units;

/// <summary>
/// The arguments of <c>admin root</c>: a username and an e-mail, host settings aside, and the
/// password on the first line of the standard input, from 12 characters to 4096 bytes.
/// </summary>
public sealed class AdminRootArgumentsTests
{
    private const int ShortestPassword = 12;
    private const int LongestPasswordBytes = 4096;

    // "é" is two bytes in UTF-8: a password of them is counted in characters and in bytes.
    private const string TwoByteCharacter = "é";

    [Theory]
    [InlineData("--username", " root_admin ", "--email", "Root@Example.TEST")]
    [InlineData("--username=root_admin", "--Logging:Level=Warning", "--email=root@example.test")]
    public void TheAccountIsReadInItsStoredForm(params string[] arguments)
    {
        var parsed = AdminRootArguments.Parse(arguments);

        Assert.Equal(("root_admin", "root@example.test"), (parsed.Username, parsed.Email));
    }

    [Theory]
    [InlineData]
    [InlineData("--username", "root_admin")]
    [InlineData("--email", "root@example.test")]
    [InlineData("--username", "ra", "--email", "root@example.test")]
    [InlineData("--username", "root_admin", "--email", "not-an-email")]
    [InlineData("--username", "root_admin", "--email", "a@b.test", "--password", "x")]
    [InlineData("--username", "root_admin", "--username=other", "--email", "a@b.test")]
    [InlineData("--username", "root_admin", "--email")]
    public void AnyOtherUseIsRefused(params string[] arguments) =>
        Assert.Throws<FormatException>(() => AdminRootArguments.Parse(arguments));

    public static TheoryData<string> UsablePasswords => new()
    {
        new string('a', ShortestPassword),
        new string('a', ShortestPassword) + "\r",
        string.Concat(Enumerable.Repeat(TwoByteCharacter, LongestPasswordBytes / 2)),
    };

    // No line at all reads as an empty one.
    public static TheoryData<string> UnusablePasswords => new()
    {
        string.Empty,
        new string('a', ShortestPassword - 1),
        string.Concat(Enumerable.Repeat(TwoByteCharacter, ShortestPassword / 2)),
        string.Concat(Enumerable.Repeat(TwoByteCharacter, LongestPasswordBytes / 2)) + "a",
    };

    [Theory]
    [MemberData(nameof(UsablePasswords))]
    public void PasswordOfTwelveCharactersUpToSymfonysBoundIsKept(string line) =>
        Assert.Equal(line.TrimEnd('\r'), AdminRootArguments.Password(line));

    [Theory]
    [MemberData(nameof(UnusablePasswords))]
    public void ShorterOrLongerPasswordIsRefused(string line) =>
        Assert.Throws<FormatException>(() => AdminRootArguments.Password(line));
}
