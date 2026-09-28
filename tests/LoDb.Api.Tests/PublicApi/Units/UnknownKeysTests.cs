using System.Globalization;
using LoDb.Api.Modules.PublicApi;
using LoDb.Api.Modules.PublicApi.Access;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace LoDb.Api.Tests.PublicApi.Units;

/// <summary>
/// A made-up key costs one read of the database per lifetime, within a bounded memory, and
/// never outlives a change of the keys.
/// </summary>
public sealed class UnknownKeysTests
{
    private const string Hash = "4f8c091149898ec90c0a64643840af0cbc27a664";
    private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(1);

    private readonly FakeTimeProvider _clock = new();
    private readonly UnknownKeys _unknown;

    public UnknownKeysTests() =>
        _unknown = new UnknownKeys(
            _clock,
            Options.Create(new PublicApiOptions { KeyCacheLifetime = Lifetime }));

    [Fact]
    public void AnUnknownKeyIsRememberedForALifetime()
    {
        _unknown.Add(Hash, 0);
        _clock.Advance(Lifetime - TimeSpan.FromTicks(1));
        Assert.True(_unknown.Contains(Hash, 0));

        _clock.Advance(TimeSpan.FromTicks(1));

        Assert.False(_unknown.Contains(Hash, 0));
    }

    [Fact]
    public void AChangeOfTheKeysForgetsIt()
    {
        _unknown.Add(Hash, 0);

        Assert.False(_unknown.Contains(Hash, 1));
        Assert.False(_unknown.Contains(Hash, 0));
    }

    [Fact]
    public void ClearingForgetsThemAll()
    {
        _unknown.Add(Hash, 0);

        _unknown.Clear();

        Assert.False(_unknown.Contains(Hash, 0));
    }

    [Fact]
    public void OnceFullOnlyTheExpiredMakeRoom()
    {
        for (var index = 0; index < UnknownKeys.MaxEntries; index++)
        {
            _unknown.Add(index.ToString(CultureInfo.InvariantCulture), 0);
        }

        _unknown.Add(Hash, 0);
        Assert.False(_unknown.Contains(Hash, 0));
        Assert.True(_unknown.Contains("0", 0));

        _clock.Advance(Lifetime);
        _unknown.Add(Hash, 0);

        Assert.True(_unknown.Contains(Hash, 0));
    }
}
