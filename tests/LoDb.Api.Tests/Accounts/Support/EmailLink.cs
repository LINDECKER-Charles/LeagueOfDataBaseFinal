using System.Globalization;
using LoDb.Infrastructure.Outbox;
using Microsoft.AspNetCore.WebUtilities;

namespace LoDb.Api.Tests.Accounts.Support;

/// <summary>The link of an account e-mail, split as its front page reads it.</summary>
/// <param name="Page">The link without its query.</param>
/// <param name="UserId">The <c>user</c> parameter.</param>
/// <param name="Token">The <c>token</c> parameter, sent back as is.</param>
public sealed record EmailLink(Uri Page, int UserId, string Token)
{
    public const string ActionUrlKey = "actionUrl";

    public static EmailLink Of(EmailMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);
        var url = new Uri(message.Model[ActionUrlKey]!);
        var query = QueryHelpers.ParseQuery(url.Query);
        return new EmailLink(
            new Uri(url.GetLeftPart(UriPartial.Path)),
            int.Parse(query["user"].ToString(), CultureInfo.InvariantCulture),
            query["token"].ToString());
    }
}
