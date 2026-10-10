using LoDb.Api.Hosting.Secrets;
using Microsoft.Extensions.Configuration;

namespace LoDb.Api.Tests.Hosting.Secrets;

/// <summary>
/// The settings of a folder of secret files: the files named after a setting key, with a
/// content; an empty file stands for an unset setting, as the deploy job writes it.
/// </summary>
public sealed class SecretFileSetTests : IDisposable
{
    private readonly SecretFolder _folder = new();

    [Fact]
    public void ReadsTheFilesNamedAfterASettingThatHoldAValue()
    {
        _folder
            .With("LoDb:Billing:StripeSecretKey", "sk_test_value")
            .With("LoDb:Accounts:Google:AppClients:0:ClientSecret", "secret")
            .With("ConnectionStrings:LoDb", "Host=postgres")
            .With("LoDb:Mail:Password", string.Empty)
            .WithFile("data-protection.pfx", "certificate")
            .WithFile(".LoDb__Mail__Host.staged", "relay")
            .WithFile("LODB_DB_PASSWORD", "password")
            .WithFile("LoDb__Mail_Host", "relay");
        Directory.CreateDirectory(Path.Combine(_folder.Path, "LoDb__Nested__Folder"));

        var secretFiles = SecretFileSet.Read(Configured(_folder.Path));

        Assert.Equal(new DirectoryInfo(_folder.Path).FullName, secretFiles.Folder);
        Assert.Equal(
            [
                "ConnectionStrings:LoDb",
                "LoDb:Accounts:Google:AppClients:0:ClientSecret",
                "LoDb:Billing:StripeSecretKey",
            ],
            secretFiles.SettingKeys);
    }

    [Fact]
    public void MissingFolderHoldsNoSetting()
    {
        var missing = Path.Combine(_folder.Path, "missing");

        var secretFiles = SecretFileSet.Read(Configured(missing));

        Assert.Empty(secretFiles.FileNames);
    }

    [Fact]
    public void UnsetFolderIsTheOneComposeMounts()
    {
        var secretFiles = SecretFileSet.Read(new ConfigurationBuilder().Build());

        Assert.Equal(Path.GetFullPath(SecretFileSet.DefaultFolder), secretFiles.Folder);
    }

    public void Dispose() => _folder.Dispose();

    private static IConfiguration Configured(string folder) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection([new(SecretFileSet.FolderKey, folder)])
            .Build();
}
