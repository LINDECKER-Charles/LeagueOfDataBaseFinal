using LoDb.Api.Cli.Admin;

namespace LoDb.Api.Tests.Admin.Units;

/// <summary>
/// The arguments of <c>admin create</c>, <c>mfa-invite</c> and <c>mfa-reset</c>: one e-mail,
/// host settings aside.
/// </summary>
public sealed class AdminEmailArgumentsTests
{
    [Theory]
    [InlineData("--email", "Admin@Example.test")]
    [InlineData("--email=Admin@Example.test")]
    [InlineData("--ConnectionStrings:LoDb=Host=db", "--email", " admin@example.test ")]
    public void TheEmailIsReadInItsStoredForm(params string[] arguments)
    {
        var parsed = AdminEmailArguments.Parse(arguments);

        Assert.Equal("admin@example.test", parsed.Email);
    }

    [Theory]
    [InlineData]
    [InlineData("--email")]
    [InlineData("--email", "--Logging:LogLevel:Default=Warning")]
    [InlineData("--email", "nobody")]
    [InlineData("--email=a@example.test", "--email=b@example.test")]
    [InlineData("--mail", "a@example.test")]
    [InlineData("admin@example.test")]
    [InlineData("--verbose=true", "--email", "a@example.test")]
    public void AnythingElseIsRefused(params string[] arguments)
    {
        Assert.Throws<FormatException>(() => AdminEmailArguments.Parse(arguments));
    }
}
