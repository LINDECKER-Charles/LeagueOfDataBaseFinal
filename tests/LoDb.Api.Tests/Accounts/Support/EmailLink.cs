using System.Globalization;
using LoDb.Infrastructure.Outbox;
using Microsoft.AspNetCore.WebUtilities;

namespace LoDb.Api.Tests.Accounts.Support;

/// <summary>The link of an account e-mail, split as its front page reads it.</summary>
/// <param name="Page">The page of the front route: the link without its query or token.</param>
/// <param name="UserId">The <c>user</c> parameter.</param>
/// <param name="Token">
/// The token, sent back as is: the <c>token</c> parameter of the verification link, the last
/// segment of the reset link (<c>reset-password/:token</c>).
/// </param>
public sealed record EmailLink(Uri Page, int UserId, string Token)
{
    public static EmailLink Of(EmailMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);
        return Of(new Uri(message.Model[EmailModelKeys.ActionUrl]!));
    }

    public static EmailLink Of(Uri url)
    {
        ArgumentNullException.ThrowIfNull(url);
        var query = QueryHelpers.ParseQuery(url.Query);
        var userId = int.Parse(query["user"].ToString(), CultureInfo.InvariantCulture);
        var path = url.GetLeftPart(UriPartial.Path);
        if (query.TryGetValue("token", out var token))
        {
            return new EmailLink(new Uri(path), userId, token.ToString());
        }

        var slash = path.LastIndexOf('/');
        return new EmailLink(
            new Uri(path[..slash]),
            userId,
            Uri.UnescapeDataString(path[(slash + 1)..]));
    }
}
