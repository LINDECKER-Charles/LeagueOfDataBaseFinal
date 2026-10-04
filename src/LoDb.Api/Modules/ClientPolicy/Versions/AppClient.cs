namespace LoDb.Api.Modules.ClientPolicy.Versions;

/// <summary>An app as its <c>X-LoDb-Client</c> header names it.</summary>
internal sealed record AppClient(ClientPlatform Platform, ClientVersion Version);
