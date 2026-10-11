using System.Globalization;
using System.Net;
using LoDb.Api.Tests.Accounts.Support;
using LoDb.Api.Tests.Contact.Support;
using LoDb.Testing;

namespace LoDb.Api.Tests.Contact;

/// <summary>
/// <c>POST /api/contact</c> on a host of its own: without a mailbox the message is only kept,
/// and a signed-in sender is recorded with it.
/// </summary>
public sealed class ContactOwnHostTests(PostgresContainerFixture postgres)
{
    [Fact]
    public async Task WithoutARecipientTheMessageIsOnlyKept()
    {
        await using var app = await ContactApp.StartAsync(postgres, recipient: "  ");
        using var browser = app.Browser();

        using var response = await browser.PostAsync(ContactForm.Path, ContactForm.Valid());

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Single(await app.MessagesAsync());
        Assert.Empty(app.Outbox.Messages);
    }

    [Fact]
    public async Task SignedInSenderIsRecordedWithItsMessage()
    {
        await using var accounts = await AccountsApp.StartAsync(postgres, withGoogle: false);
        var user = await accounts.SeedAsync(new AccountSeed());
        using var browser = accounts.Browser();
        using var signIn = await browser.SignInAsync(AccountSeed.Name);

        using var response = await browser.PostAsync(ContactForm.Path, ContactForm.Valid());

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(
            [user.Id.ToString(CultureInfo.InvariantCulture)],
            await accounts.QueryAsync("SELECT user_id FROM contact_messages"));
    }
}
