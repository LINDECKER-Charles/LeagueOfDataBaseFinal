namespace LoDb.Infrastructure.Tests.Audience.Support;

/// <summary>A request <see cref="FakeUmami"/> received.</summary>
internal sealed record FakeUmamiRequest(
    HttpMethod Method,
    Uri Url,
    string? Authorization,
    string? Body);
