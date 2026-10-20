namespace LoDb.Api.Tests.Prerender.Support;

/// <summary>A request the fake SSR received: its path, and its headers by name.</summary>
internal sealed record SsrRequest(string Path, IReadOnlyDictionary<string, string> Headers);
