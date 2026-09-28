namespace LoDb.Infrastructure.Outbox.Delivery;

/// <summary>How a failed attempt is recorded.</summary>
/// <param name="ErrorCode">What failed (<see cref="DeliveryFailure.Code"/>).</param>
/// <param name="Dead">True when the message is given up.</param>
/// <param name="NextAttemptAt">When a message not given up is due again.</param>
internal sealed record FailedAttempt(string ErrorCode, bool Dead, DateTimeOffset NextAttemptAt);
