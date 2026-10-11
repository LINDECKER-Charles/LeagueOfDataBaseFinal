using System.Globalization;
using LoDb.Infrastructure.Outbox;
using Microsoft.AspNetCore.WebUtilities;

namespace LoDb.Api.Tests.Accounts.Support;

/// <summary>The link of an account e-mail, split as its front page reads it.</summary>
/// <param name="Page">The page of the front route: the link without its query or fragment.</param>
/// <param name="UserId">The <c>user</c> parameter of the query.</param>
/// <param name="Token">
/// The token, sent back as is: the <c>token</c> parameter of the fragment, which the browser
/// never sends to a server.
/// </param>
public sealed record EmailLink(Uri Page, int UserId, string Token)
{
    private const char FragmentMark = '#';

    public static EmailLink Of(EmailMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);
        return Of(new Uri(message.Model[EmailModelKeys.ActionUrl]!));
    }

    public static EmailLink Of(Uri url)
    {
        ArgumentNullException.ThrowIfNull(url);
        var query = QueryHelpers.ParseQuery(url.Query);
        var fragment = QueryHelpers.ParseQuery(url.Fragment.TrimStart(FragmentMark));
        return new EmailLink(
            new Uri(url.GetLeftPart(UriPartial.Path)),
            int.Parse(query["user"].ToString(), CultureInfo.InvariantCulture),
            fragment["token"].ToString());
    }
}
