using System.Globalization;
using System.Net;
using LoDb.Api.Tests.Accounts.Support;
using LoDb.Api.Tests.Admin.Support;
using LoDb.Infrastructure.Audit;
using Microsoft.EntityFrameworkCore;

namespace LoDb.Api.Tests.Admin;

/// <summary>
/// <c>/api/admin/builds</c> and <c>/api/admin/contacts</c>: a build row, the builds
/// unpublished or deleted, an unknown contact message. The searches, filters and counters
/// of the lists are <see cref="OwnHost.AdminListsTests"/>'.
/// </summary>
/// <remarks>
/// The tests share a host, each with an administrator, an author and builds of its own.
/// </remarks>
public sealed class AdminContentTests(SharedAccountsApp host)
    : AdminTestBase(host), IClassFixture<SharedAccountsApp>
{
    private const string BuildsPath = "/api/admin/builds";
    private const string ContactsPath = "/api/admin/contacts";

    private readonly AccountSeed _admin = host.Unique();
    private readonly AccountSeed _owner = host.Unique();

    // A build name no other test of the host uses: its champion, then its author.
    private string BuildName => "Ahri " + _owner.Username;

    [Fact]
    public async Task ABuildRowLinksItsSharePageAndFlagsABannedAuthor()
    {
        using var admin = await AdminCalls.OpenEnrolledAsync(App, _admin);
        var owner = await App.SeedAsync(_owner with { Banned = true });
        await AdminSeed.BuildAsync(App, owner.Id, BuildName);

        var page = await ReadAsync(admin, $"{BuildsPath}?q={Uri.EscapeDataString(BuildName)}");

        var row = Assert.Single(page.GetProperty("items").EnumerateArray());
        Assert.Equal("share-" + BuildName, ApiJson.Text(row, "shareToken"));
        Assert.True(row.GetProperty("owner").GetProperty("isBanned").GetBoolean());
    }

    [Fact]
    public async Task UnpublishingAndDeletingABuildAreAudited()
    {
        using var admin = await AdminCalls.OpenEnrolledAsync(App, _admin);
        var owner = await App.SeedAsync(_owner);
        var build = await AdminSeed.BuildAsync(App, owner.Id, BuildName);

        using var hide = await admin.PostAsync($"{BuildsPath}/{build}/unpublish");
        var hidden = await LastAuditAsync(AuditAction.AdminBuildHide);
        var isPublic = await AdminSeed.WithContextAsync(App, context =>
            context.Builds.Where(row => row.Id == build)
                .Select(static row => row.IsPublic)
                .SingleAsync(Cancellation));
        using var deletion = await AdminCalls.DeleteAsync(admin, $"{BuildsPath}/{build}");

        Assert.Equal(HttpStatusCode.NoContent, hide.StatusCode);
        Assert.False(isPublic);
        Assert.Equal(
            (AuditTargetType.Build, build.ToString(CultureInfo.InvariantCulture), BuildName),
            (hidden.TargetType, hidden.TargetId, hidden.Target));
        Assert.Equal(HttpStatusCode.NoContent, deletion.StatusCode);
        await LastAuditAsync(AuditAction.AdminBuildDelete);
        Assert.False(await AdminSeed.WithContextAsync(App, context =>
            context.Builds.AnyAsync(row => row.Id == build, Cancellation)));
        using var gone = await AdminCalls.DeleteAsync(admin, $"{BuildsPath}/{build}");
        Assert.Equal(
            "build-not-found",
            await ApiJson.ProblemCodeAsync(gone, HttpStatusCode.NotFound));
    }

    [Fact]
    public async Task AnUnknownContactMessageIsNotFound()
    {
        using var admin = await AdminCalls.OpenEnrolledAsync(App, _admin);

        using var handle = await admin.PostAsync($"{ContactsPath}/999999/handle");

        Assert.Equal(
            "contact-not-found",
            await ApiJson.ProblemCodeAsync(handle, HttpStatusCode.NotFound));
    }
}
