namespace LoDb.Infrastructure.Outbox.Delivery;

/// <summary>A stored template or locale this version does not know.</summary>
internal sealed class UnknownStoredValueException(string message) : Exception(message);
