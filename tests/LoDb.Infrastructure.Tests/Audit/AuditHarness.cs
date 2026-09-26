using System.Globalization;
using System.Net;
using System.Security.Claims;
using LoDb.Infrastructure.Audit;
using LoDb.Infrastructure.Audit.Journal;
using LoDb.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.Extensions.Time.Testing;

namespace LoDb.Infrastructure.Tests.Audit;

/// <summary>
/// The audit zone as the API registers it, on a database and a request of the test's
/// choice, with a fixed clock and the logs of the journal kept.
/// </summary>
internal sealed class AuditHarness : IAsyncDisposable
{
    public const string Route = "/api/admin/users/{id}/ban";

    /// <summary>A client of a dual-stack socket, as Kestrel reports an IPv4 one.</summary>
    public const string MappedAddress = "::ffff:203.0.113.9";

    public static readonly DateTimeOffset Now = new(2026, 9, 26, 8, 30, 15, TimeSpan.Zero);

    private readonly ServiceProvider _services;

    public AuditHarness(string connectionString)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"ConnectionStrings:{LoDbDataSource.ConnectionStringName}"] = connectionString,
            })
            .Build();
        _services = new ServiceCollection()
            .AddSingleton<IConfiguration>(configuration)
            .AddSingleton<TimeProvider>(new FakeTimeProvider(Now))
            .AddSingleton<IHttpContextAccessor>(Requests)
            .AddFakeLogging()
            .AddLoDbPersistence(configuration)
            .AddLoDbAudit(configuration)
            .BuildServiceProvider(validateScopes: true);
    }

    public HttpContextAccessor Requests { get; } = new();

    public IAuditLog Journal => _services.GetRequiredService<IAuditLog>();

    /// <summary>What the journal logged: its mirror and its failures.</summary>
    public IReadOnlyList<FakeLogRecord> Records =>
    [
        .. _services.GetFakeLogCollector().GetSnapshot()
            .Where(static record => record.Category == typeof(AuditLog).FullName),
    ];

    /// <summary>A request of <paramref name="user"/> on <see cref="Route"/>.</summary>
    public static DefaultHttpContext Request(ClaimsPrincipal user)
    {
        var context = new DefaultHttpContext { User = user };
        context.Connection.RemoteIpAddress = IPAddress.Parse(MappedAddress);
        context.SetEndpoint(new RouteEndpoint(
            static _ => Task.CompletedTask,
            RoutePatternFactory.Parse(Route),
            order: 0,
            EndpointMetadataCollection.Empty,
            displayName: null));
        return context;
    }

    /// <summary>An account signed in as Identity's claims factory describes it.</summary>
    public static ClaimsPrincipal SignedIn(int id, string userName, params string[] roles) =>
        new(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, id.ToString(CultureInfo.InvariantCulture)),
                new Claim(ClaimTypes.Name, userName),
                .. roles.Select(static role => new Claim(ClaimTypes.Role, role)),
            ],
            authenticationType: "Identity.Application"));

    public ValueTask DisposeAsync() => _services.DisposeAsync();
}
