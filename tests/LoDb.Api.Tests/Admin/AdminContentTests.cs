using System.Globalization;
using System.Net;
using System.Text.Json;
using LoDb.Api.Tests.Accounts.Support;
using LoDb.Api.Tests.Admin.Support;
using LoDb.Api.Tests.Audit.Support;
using LoDb.Infrastructure.Audit;
using LoDb.Testing;
using Microsoft.EntityFrameworkCore;

namespace LoDb.Api.Tests.Admin;

/// <summary>
/// <c>/api/admin/builds</c> and <c>/api/admin/contacts</c>: the builds unpublished or deleted,
/// the contact messages handled, reopened or deleted.
/// </summary>
public sealed class AdminContentTests(PostgresContainerFixture postgres)
    : AdminTestBase(postgres), IClassFixture<PostgresContainerFixture>
{
    private const string BuildsPath = "/api/admin/builds";
    private const string ContactsPath = "/api/admin/contacts";

    [Fact]
    public async Task BuildsAreSearchedByNameAndVisibility()
    {
        using var admin = await AdminBrowser.OpenAsync(App);
        var owner = await App.SeedAsync(new AccountSeed());
        var ahri = await AdminSeed.BuildAsync(App, owner.Id, "Ahri mid");
        await AdminSeed.BuildAsync(App, owner.Id, "Garen top");
        using var hide = await admin.PostAsync($"{BuildsPath}/{ahri}/unpublish");

        var byName = await ReadAsync(admin, BuildsPath + "?q=AHRI");
        var hidden = await ReadAsync(admin, BuildsPath + "?visibility=private");
        var shown = await ReadAsync(admin, BuildsPath + "?visibility=public");

        var found = Assert.Single(byName.GetProperty("items").EnumerateArray());
        Assert.Equal(
            (ahri, false, AccountSeed.Name),
            (found.GetProperty("id").GetInt32(), found.GetProperty("isPublic").GetBoolean(),
                ApiJson.Text(found.GetProperty("owner"), "username")));
        Assert.Equal(ahri, Assert.Single(hidden.GetProperty("items").EnumerateArray())
            .GetProperty("id").GetInt32());
        Assert.Equal("Garen top", ApiJson.Text(
            Assert.Single(shown.GetProperty("items").EnumerateArray()), "name"));
        var stats = shown.GetProperty("stats");
        Assert.Equal(
            (2, 1),
            (stats.GetProperty("total").GetInt32(), stats.GetProperty("public").GetInt32()));
    }

    [Fact]
    public async Task ABuildRowLinksItsSharePageAndFlagsABannedAuthor()
    {
        using var admin = await AdminBrowser.OpenAsync(App);
        var owner = await App.SeedAsync(new AccountSeed { Banned = true });
        await AdminSeed.BuildAsync(App, owner.Id, "Ahri mid");

        var page = await ReadAsync(admin, BuildsPath);

        var row = Assert.Single(page.GetProperty("items").EnumerateArray());
        Assert.Equal("share-Ahri mid", ApiJson.Text(row, "shareToken"));
        Assert.True(row.GetProperty("owner").GetProperty("isBanned").GetBoolean());
    }

    [Fact]
    public async Task UnpublishingAndDeletingABuildAreAudited()
    {
        using var admin = await AdminBrowser.OpenAsync(App);
        var owner = await App.SeedAsync(new AccountSeed());
        var build = await AdminSeed.BuildAsync(App, owner.Id, "Ahri mid");

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
            (AuditTargetType.Build, build.ToString(CultureInfo.InvariantCulture), "Ahri mid"),
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
    public async Task ContactMessagesAreHandledReopenedAndDeleted()
    {
        using var admin = await AdminBrowser.OpenAsync(App);
        var message = await AdminSeed.ContactAsync(App, "Lien cassé");

        var fresh = await ReadAsync(admin, ContactsPath + "?status=new");
        using var handle = await admin.PostAsync($"{ContactsPath}/{message}/handle");
        var handled = await ReadAsync(admin, ContactsPath + "?status=handled");
        using var reopen = await admin.PostAsync($"{ContactsPath}/{message}/reopen");
        var reopened = await ReadAsync(admin, ContactsPath);
        using var deletion = await AdminCalls.DeleteAsync(admin, $"{ContactsPath}/{message}");

        Assert.Equal("Lien cassé", ApiJson.Text(
            Assert.Single(fresh.GetProperty("items").EnumerateArray()), "subject"));
        Assert.Equal(
            (HttpStatusCode.NoContent, HttpStatusCode.NoContent, HttpStatusCode.NoContent),
            (handle.StatusCode, reopen.StatusCode, deletion.StatusCode));
        var done = Assert.Single(handled.GetProperty("items").EnumerateArray());
        Assert.Equal("handled", ApiJson.Text(done, "status"));
        Assert.NotEqual(JsonValueKind.Null, done.GetProperty("handledAt").ValueKind);
        var open = Assert.Single(reopened.GetProperty("items").EnumerateArray());
        Assert.Equal("new", ApiJson.Text(open, "status"));
        Assert.Equal(JsonValueKind.Null, open.GetProperty("handledAt").ValueKind);
        Assert.Equal(1, reopened.GetProperty("stats").GetProperty("new").GetInt32());
        Assert.Equal(0, (await ReadAsync(admin, ContactsPath)).GetProperty("total").GetInt32());
    }

    [Fact]
    public async Task AnUnknownContactMessageIsNotFound()
    {
        using var admin = await AdminBrowser.OpenAsync(App);

        using var handle = await admin.PostAsync($"{ContactsPath}/999999/handle");

        Assert.Equal(
            "contact-not-found",
            await ApiJson.ProblemCodeAsync(handle, HttpStatusCode.NotFound));
    }
}
