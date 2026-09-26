using System.Globalization;
using LoDb.Api.Modules.Accounts.Authentication;
using LoDb.Domain.Languages;
using LoDb.Infrastructure.Outbox;
using LoDb.Infrastructure.Persistence.Accounts;
using Microsoft.AspNetCore.Identity;

namespace LoDb.Api.Modules.Accounts.Links;

/// <summary>
/// Queues the e-mails of the accounts in the outbox: the link that verifies an e-mail, and
/// the one that sets a new password. Both live an hour.
/// </summary>
/// <remarks>
/// The model holds the keys of <see cref="EmailModelKeys"/> the templates read: the username,
/// the link and its lifetime in minutes.
/// </remarks>
internal sealed partial class AccountMail(
    UserManager<User> users,
    LinkOrigin origin,
    IServiceProvider services,
    ILogger<AccountMail> logger)
{
    // Whole minutes in invariant digits, as the templates read them.
    private static readonly string LifetimeMinutes =
        ((int)AuthenticationSetup.EmailTokenLifetime.TotalMinutes)
            .ToString(CultureInfo.InvariantCulture);

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
        [EmailModelKeys.UserName] = letter.Account.UserName,
        [EmailModelKeys.ActionUrl] = site + Link(letter),
        [EmailModelKeys.ExpiresInMinutes] = LifetimeMinutes,
    };

    private static string Link(Letter letter)
    {
        var token = EmailTokens.Encode(letter.Token);
        return letter.Template == EmailTemplate.ConfirmEmail
            ? FrontPages.VerifyEmailLink(letter.Locale, letter.Account.Id, token)
            : FrontPages.ResetPasswordLink(letter.Locale, letter.Account.Id, token);
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
