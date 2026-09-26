using System.Text;
using LoDb.Desktop.Auth.Tokens;
using LoDb.Desktop.Tests.Support;
using Microsoft.Extensions.DependencyInjection;

namespace LoDb.Desktop.Tests.Auth;

public sealed class RefreshTokenVaultTests
{
    private const string RefreshToken = "CfDJ8-refresh-token-of-thirty-days";

    [Fact]
    public async Task EncryptsTheTokenAtRestAndReadsItBack()
    {
        await using var host = await StartAsync();
        var vault = host.Services.GetRequiredService<RefreshTokenVault>();

        vault.Save(RefreshToken);
        var saved = await File.ReadAllBytesAsync(
            vault.FilePath,
            TestContext.Current.CancellationToken);

        Assert.DoesNotContain(
            RefreshToken,
            Encoding.UTF8.GetString(saved),
            StringComparison.Ordinal);
        Assert.Equal(RefreshToken, vault.Load());
    }

    [Fact]
    public async Task ReadsNoTokenWhenThereIsNone()
    {
        await using var host = await StartAsync();

        Assert.Null(host.Services.GetRequiredService<RefreshTokenVault>().Load());
    }

    [Fact]
    public async Task ReadsAnAlteredFileAsNoToken()
    {
        await using var host = await StartAsync();
        var vault = host.Services.GetRequiredService<RefreshTokenVault>();
        vault.Save(RefreshToken);
        var saved = await File.ReadAllBytesAsync(
            vault.FilePath,
            TestContext.Current.CancellationToken);
        saved[^1] ^= 0xFF;
        await File.WriteAllBytesAsync(vault.FilePath, saved, TestContext.Current.CancellationToken);

        Assert.Null(vault.Load());
    }

    [Fact]
    public async Task ReadsAFileOfOtherKeysAsNoToken()
    {
        await using var first = await StartAsync();
        await using var second = await StartAsync();
        var theirs = first.Services.GetRequiredService<RefreshTokenVault>();
        var ours = second.Services.GetRequiredService<RefreshTokenVault>();
        theirs.Save(RefreshToken);
        Directory.CreateDirectory(Path.GetDirectoryName(ours.FilePath)!);
        File.Copy(theirs.FilePath, ours.FilePath);

        Assert.Null(ours.Load());
    }

    [Fact]
    public async Task KeepsTheSessionFolderToItsOwner()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "Unix permissions only.");
        await using var host = await StartAsync();
        var vault = host.Services.GetRequiredService<RefreshTokenVault>();

        vault.Save(RefreshToken);

        Assert.Equal(
            UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute,
            OwnerModeOf(Path.GetDirectoryName(vault.FilePath)!));
    }

    private static UnixFileMode OwnerModeOf(string path) =>
        OperatingSystem.IsWindows() ? UnixFileMode.None : File.GetUnixFileMode(path);

    private static Task<DesktopTestHost> StartAsync() =>
        DesktopTestHost.StartAsync(
            new DesktopHostSetup { ApiOrigin = new Uri("http://127.0.0.1:1") },
            TestContext.Current.CancellationToken);
}
