namespace LoDb.Api.Modules.Billing.Webhooks;

/// <summary>What became of a verified event.</summary>
internal enum WebhookOutcome
{
    /// <summary>Its effects were applied and it was recorded.</summary>
    Processed,

    /// <summary>A type the site does not handle, recorded without effect.</summary>
    Ignored,

    /// <summary>Already recorded: a redelivery, applied no second time.</summary>
    Duplicate,

    /// <summary>Its handler failed: nothing was kept, and Stripe will deliver it again.</summary>
    Failed,
}
