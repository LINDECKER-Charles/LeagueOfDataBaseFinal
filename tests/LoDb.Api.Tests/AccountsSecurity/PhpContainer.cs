using System.Text.Json;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;

namespace LoDb.Api.Tests.AccountsSecurity;

/// <summary>
/// The PHP of the legacy stack, without any network, running
/// <c>tests/fixtures/hashes/verify.php</c> on hashes made here.
/// </summary>
public sealed class PhpContainer : IAsyncLifetime
{
    /// <summary>The image of <c>tests/fixtures/hashes/generate.sh</c>.</summary>
    public const string Image = "php:8.5-cli";

    private const string Fixtures = "/fixtures/";
    private const string Script = Fixtures + "verify.php";
    private const string Cases = Fixtures + "cases.json";

    private readonly IContainer _container = new ContainerBuilder(Image)
        // Kept alive between the checks; its init stops it at once at the end.
        .WithEntrypoint("sleep", "infinity")
        .WithResourceMapping(
            new FileInfo(Path.Combine(PhpHashes.Directory, "verify.php")),
            Fixtures)
        .WithCreateParameterModifier(static parameters =>
        {
            parameters.HostConfig ??= new();
            parameters.HostConfig.NetworkMode = "none";
            parameters.HostConfig.Init = true;
        })
        .Build();

    /// <summary>
    /// What <c>password_verify</c> and Symfony's check answer for each password and hash.
    /// </summary>
    public async Task<IReadOnlyList<(bool PasswordVerify, bool Symfony)>> VerifyAsync(
        IEnumerable<(string Password, string Hash)> cases,
        CancellationToken cancellationToken)
    {
        var json = JsonSerializer.SerializeToUtf8Bytes(cases.Select(static item =>
            new Dictionary<string, string> { ["password"] = item.Password, ["hash"] = item.Hash }));
        await _container.CopyAsync(json, Cases, ct: cancellationToken);

        var result = await _container.ExecAsync(["php", Script, Cases], cancellationToken);
        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException($"verify.php failed: {result.Stderr}");
        }

        using var verdicts = JsonDocument.Parse(result.Stdout);
        return
        [
            .. verdicts.RootElement.EnumerateArray().Select(static verdict => (
                verdict.GetProperty("password_verify").GetBoolean(),
                verdict.GetProperty("symfony").GetBoolean())),
        ];
    }

    /// <summary>The PHP version of the container.</summary>
    public async Task<string> VersionAsync(CancellationToken cancellationToken)
    {
        var result = await _container.ExecAsync(
            ["php", "-r", "echo PHP_VERSION;"],
            cancellationToken);
        return result.Stdout;
    }

    public async ValueTask InitializeAsync() =>
        await _container.StartAsync(TestContext.Current.CancellationToken);

    public ValueTask DisposeAsync() => _container.DisposeAsync();
}
