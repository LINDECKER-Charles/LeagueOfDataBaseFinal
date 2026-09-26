using LoDb.Api.Cli;
using LoDb.Api.Hosting;
using LoDb.Api.Modules.Accounts;
using LoDb.Api.Modules.Admin;
using LoDb.Api.Modules.Analytics;
using LoDb.Api.Modules.Audit;
using LoDb.Api.Modules.Billing;
using LoDb.Api.Modules.Builds;
using LoDb.Api.Modules.Catalog;
using LoDb.Api.Modules.ClientPolicy;
using LoDb.Api.Modules.Contact;
using LoDb.Api.Modules.Legacy;
using LoDb.Api.Modules.Profiles;
using LoDb.Api.Modules.PublicApi;
using LoDb.Api.Modules.Seo;
using LoDb.Api.Modules.Trends;
using LoDb.Api.Workers;
using LoDb.Infrastructure.Analytics;
using LoDb.Infrastructure.Audit;
using LoDb.Infrastructure.DataProtection;
using LoDb.Infrastructure.Jobs;
using LoDb.Infrastructure.Outbox;
using LoDb.Infrastructure.Persistence;
using LoDb.Infrastructure.Storage;
using LoDb.Ingestion.Catalog;
using LoDb.Ingestion.Ddragon;
using LoDb.Ingestion.Egress;
using LoDb.Ingestion.Pipeline;

// A sub-command is picked before any web host exists: healthcheck must open no port, and
// the other commands run in a generic host without Kestrel.
if (CliRunner.IsCommand(args))
{
    return await CliRunner.RunAsync(args, typeof(Program).Assembly, AddLoDbServices);
}

var builder = WebApplication.CreateBuilder(args);
builder.AddLoDbHosting();
AddLoDbServices(builder.Services, builder.Configuration);
builder.Services.AddConventionalWorkers(builder.Configuration, typeof(Program).Assembly);

var app = builder.Build();
app.UseLoDbHosting();
MapLoDbModules(app);
app.MapLoDbHostingEndpoints();
await app.RunAsync();
return CliExitCodes.Success;

// Shared by the web host and the command host, so that a command sees the same services.
static void AddLoDbServices(IServiceCollection services, IConfiguration configuration)
{
    AddLoDbZones(services, configuration);
    AddLoDbModules(services, configuration);
}

static void AddLoDbZones(IServiceCollection services, IConfiguration configuration) =>
    services
        .AddLoDbEgress(configuration)
        .AddLoDbStorage(configuration)
        .AddLoDbPersistence(configuration)
        .AddLoDbJobs(configuration)
        .AddLoDbDdragon(configuration)
        .AddLoDbIngestion(configuration)
        .AddLoDbCatalog(configuration)
        .AddLoDbOutbox(configuration)
        .AddLoDbAudit(configuration)
        .AddLoDbDataProtection(configuration)
        .AddLoDbAnalytics(configuration);

static void AddLoDbModules(IServiceCollection services, IConfiguration configuration) =>
    services
        .AddCatalog(configuration)
        .AddSeo(configuration)
        .AddLegacy(configuration)
        .AddAccounts(configuration)
        .AddProfiles(configuration)
        .AddBuilds(configuration)
        .AddTrends(configuration)
        .AddPublicApi(configuration)
        .AddBilling(configuration)
        .AddAnalytics(configuration)
        .AddAudit(configuration)
        .AddAdmin(configuration)
        .AddContact(configuration)
        .AddClientPolicy(configuration);

static void MapLoDbModules(IEndpointRouteBuilder endpoints) =>
    endpoints
        .MapCatalog()
        .MapSeo()
        .MapLegacy()
        .MapAccounts()
        .MapProfiles()
        .MapBuilds()
        .MapTrends()
        .MapPublicApi()
        .MapBilling()
        .MapAnalytics()
        .MapAudit()
        .MapAdmin()
        .MapContact()
        .MapClientPolicy();
