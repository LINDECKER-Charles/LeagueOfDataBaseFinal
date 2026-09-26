using System.Globalization;
using LoDb.Api.Modules.Accounts.Authentication;
using LoDb.Domain.Languages;
using LoDb.Infrastructure.Outbox;
using LoDb.Infrastructure.Persistence.Accounts;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;

namespace LoDb.Api.Modules.Accounts.Links;

/// <summary>
/// Queues the e-mails of the accounts in the outbox: the link that verifies an e-mail, and
/// the one that sets a new password. Both live an hour.
/// </summary>
/// <remarks>
/// The model of the templates holds <c>username</c>, <c>displayName</c> (the username and its
/// Riot tag line), <c>actionUrl</c> and <c>expiresInMinutes</c>.
/// </remarks>
internal sealed partial class AccountMail(
    UserManager<User> users,
    LinkOrigin origin,
    IServiceProvider services,
    ILogger<AccountMail> logger)
{
    private const string UsernameKey = "username";
    private const string DisplayNameKey = "displayName";
    private const string ActionUrlKey = "actionUrl";
    private const string ExpiresInMinutesKey = "expiresInMinutes";
    private const char TaglineSeparator = '#';

    /// <summary>Queues the link verifying the e-mail of <paramref name="user"/>.</summary>
    /// <returns>Whether it is queued: not without an outbox or a site origin.</returns>
    public async Task<bool> SendConfirmationAsync(
        User user,
        UiLocale locale,
        CancellationToken cancellationToken)
    {
        var letter = new Letter
        {
            Account = user,
            Template = EmailTemplate.ConfirmEmail,
            Locale = locale,
            Token = await users.GenerateEmailConfirmationTokenAsync(user),
        };
        return await SendAsync(letter, cancellationToken);
    }

    /// <summary>Queues the link setting a new password for <paramref name="user"/>.</summary>
    /// <returns>Whether it is queued: not without an outbox or a site origin.</returns>
    public async Task<bool> SendPasswordResetAsync(
        User user,
        UiLocale locale,
        CancellationToken cancellationToken)
    {
        var letter = new Letter
        {
            Account = user,
            Template = EmailTemplate.ResetPassword,
            Locale = locale,
            Token = await users.GeneratePasswordResetTokenAsync(user),
        };
        return await SendAsync(letter, cancellationToken);
    }

    // Added to the caller's context: its next save writes the e-mail with the change.
    private async Task<bool> SendAsync(Letter letter, CancellationToken cancellationToken)
    {
        // Resolved late: the command host builds these services without any outbox.
        var outbox = services.GetService<IEmailOutbox>();
        if (outbox is null)
        {
            LogOutboxMissing(logger);
            return false;
        }

        if (origin.Resolve() is not { } site)
        {
            LogSiteOriginMissing(logger);
            return false;
        }

        if (letter.Account.Email is not { Length: > 0 } recipient)
        {
            return false;
        }

        await outbox.EnqueueAsync(Message(letter, recipient, site), cancellationToken);
        return true;
    }

    private static EmailMessage Message(Letter letter, string recipient, string site) => new()
    {
        Recipient = recipient,
        Template = letter.Template,
        Locale = letter.Locale,
        Model = Model(letter, site),
    };

    private static Dictionary<string, string?> Model(Letter letter, string site) => new()
    {
        [UsernameKey] = letter.Account.UserName,
        [DisplayNameKey] = DisplayName(letter.Account),
        [ActionUrlKey] = Link(letter, site),
        [ExpiresInMinutesKey] = AuthenticationSetup.EmailTokenLifetime.TotalMinutes
            .ToString(CultureInfo.InvariantCulture),
    };

    private static string DisplayName(User user) =>
        user.RiotTagline is { Length: > 0 } tagline
            ? $"{user.UserName}{TaglineSeparator}{tagline}"
            : user.UserName ?? string.Empty;

    private static string Link(Letter letter, string site)
    {
        var page = letter.Template == EmailTemplate.ConfirmEmail
            ? FrontPages.VerifyEmail
            : FrontPages.ResetPassword;
        return QueryHelpers.AddQueryString(
            site + FrontPages.Path(letter.Locale, page),
            new Dictionary<string, string?>
            {
                [FrontPages.UserParameter] =
                    letter.Account.Id.ToString(CultureInfo.InvariantCulture),
                [FrontPages.TokenParameter] = EmailTokens.Encode(letter.Token),
            });
    }

    [LoggerMessage(
        EventName = "accounts.mail.outbox_missing",
        Level = LogLevel.Critical,
        Message = "Account e-mail not queued: no e-mail outbox is registered.")]
    private static partial void LogOutboxMissing(ILogger logger);

    [LoggerMessage(
        EventName = "accounts.mail.site_origin_missing",
        Level = LogLevel.Critical,
        Message = "Account e-mail not queued: LoDb:Accounts:SiteOrigin is not set.")]
    private static partial void LogSiteOriginMissing(ILogger logger);

    private sealed record Letter
    {
        public required User Account { get; init; }

        public required EmailTemplate Template { get; init; }

        public required UiLocale Locale { get; init; }

        /// <summary>Identity's token, as generated.</summary>
        public required string Token { get; init; }
    }
}
