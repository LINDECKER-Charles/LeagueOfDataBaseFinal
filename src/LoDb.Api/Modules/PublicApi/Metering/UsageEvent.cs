namespace LoDb.Api.Modules.PublicApi.Metering;

/// <summary>A billed request of a key, counted on the UTC day it was admitted.</summary>
internal readonly record struct UsageEvent(int KeyId, DateOnly Day);
