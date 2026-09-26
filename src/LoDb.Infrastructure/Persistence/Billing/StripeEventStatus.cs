namespace LoDb.Infrastructure.Persistence.Billing;

/// <summary>Outcome of a handled event, stored as <c>processed</c> or <c>ignored</c>.</summary>
public enum StripeEventStatus
{
    /// <summary>Its effects were applied with the row.</summary>
    Processed,

    /// <summary>A type the site does not handle, answered 200 without effect.</summary>
    Ignored,
}
