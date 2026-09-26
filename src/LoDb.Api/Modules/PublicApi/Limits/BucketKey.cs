namespace LoDb.Api.Modules.PublicApi.Limits;

/// <summary>
/// The partition of a token bucket: a key, at a limit. A new limit starts a new, full
/// bucket; the old one goes once idle.
/// </summary>
internal readonly record struct BucketKey(int KeyId, int PerMinute);
