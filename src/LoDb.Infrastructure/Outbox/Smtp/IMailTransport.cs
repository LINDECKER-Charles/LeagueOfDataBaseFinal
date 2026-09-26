namespace LoDb.Infrastructure.Outbox.Smtp;

/// <summary>Opens a session on the mail relay, for one batch of messages.</summary>
internal interface IMailTransport
{
    /// <summary>Connects lazily: a batch with nothing to send opens no connection.</summary>
    IMailSession OpenSession();
}
