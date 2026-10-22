using LoDb.Api.Hosting;
using LoDb.Api.Modules.Admin.ApiClients;
using LoDb.Api.Modules.Admin.Backups;
using LoDb.Api.Modules.Admin.Builds;
using LoDb.Api.Modules.Admin.Contacts;
using LoDb.Api.Modules.Admin.Donations;
using LoDb.Api.Modules.Admin.Http;
using LoDb.Api.Modules.Admin.Mfa;
using LoDb.Api.Modules.Admin.Monitoring;
using LoDb.Api.Modules.Admin.Prerender;
using LoDb.Api.Modules.Admin.Prerender.Demand.Http;
using LoDb.Api.Modules.Admin.Prerender.Http;
using LoDb.Api.Modules.Admin.Prerender.Http.Endpoints;
using LoDb.Api.Modules.Admin.Prerender.Revision.Http;
using LoDb.Api.Modules.Admin.Scaleway;
using LoDb.Api.Modules.Admin.Storage;
using LoDb.Api.Modules.Admin.Users;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace LoDb.Api.Modules.Admin;

/// <summary>
/// Admin module: the second-factor enrollment of the administrators, the moderation of
/// accounts, builds, contact messages and API keys, the donations, the monitoring, the
/// storage report, the journal of the off-host backups, the pre-rendering of the catalogue and
/// the Scaleway bill of both.
/// </summary>
/// <remarks>
/// Program.cs calls both methods from the start, so the module's owner fills this file
/// without touching a shared one. Neither method may do I/O or throw. The audit journal
/// and the analytics reports live in their own modules under the same prefix; the first
/// administrator comes from <c>admin create</c> in <c>Cli/Admin</c>.
/// </remarks>
internal static class AdminModule
{
    public static IServiceCollection AddAdmin(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddHttpContextAccessor();
        services.AddHybridCache();
        services.AddAdminPolicies();
        services.TryAddSingleton<AdminTrail>();
        services.TryAddSingleton<ProcessSampler>();
        services.TryAddScoped<AdminSession>();
        services.TryAddScoped<MfaEnrollment>();
        services.TryAddScoped<UserDirectory>();
        services.TryAddScoped<UserModeration>();
        services.TryAddScoped<BuildDirectory>();
        services.TryAddScoped<BuildModeration>();
        services.TryAddScoped<ContactInbox>();
        services.TryAddScoped<ContactModeration>();
        services.TryAddScoped<ApiClientDirectory>();
        services.TryAddScoped<ApiClientDesk>();
        services.TryAddScoped<DonationLedger>();
        services.TryAddScoped<BackupJournal>();
        services.AddOptions<BackupOptions>()
            .Bind(configuration.GetSection(BackupOptions.SectionName))
            .ValidateOnStart();
        services.TryAddEnumerable(ServiceDescriptor
            .Singleton<IValidateOptions<BackupOptions>, BackupOptionsValidator>());
        // Dormant until its settings and its store's are all there: the worker, the promotion
        // and the command line then do nothing, and the panel says so.
        services.AddAdminPrerender(configuration);
        services.AddAdminPrerenderHttp(configuration);
        // Off until its read-only key is there: the panels then say it is not configured.
        services.AddAdminScalewayBilling(configuration);
        return services.AddAdminReports();
    }

    public static IEndpointRouteBuilder MapAdmin(this IEndpointRouteBuilder endpoints)
    {
        var mfa = Group(endpoints, "/mfa", AdminRoutes.MfaTag)
            .RequireAuthorization(AdminPolicies.Enrollment);
        MfaEnrollmentEndpoint.Map(mfa);
        MfaConfirmEndpoint.Map(mfa);
        var users = Admin(endpoints, "/users", AdminRoutes.UsersTag);
        UserListEndpoint.Map(users);
        UserBanEndpoint.Map(users);
        UserUnbanEndpoint.Map(users);
        UserDeleteEndpoint.Map(users);
        var builds = Admin(endpoints, "/builds", AdminRoutes.BuildsTag);
        BuildListEndpoint.Map(builds);
        BuildUnpublishEndpoint.Map(builds);
        BuildDeleteEndpoint.Map(builds);
        var contacts = Admin(endpoints, "/contacts", AdminRoutes.ContactsTag);
        ContactListEndpoint.Map(contacts);
        ContactHandleEndpoint.Map(contacts);
        ContactReopenEndpoint.Map(contacts);
        ContactDeleteEndpoint.Map(contacts);
        var clients = Admin(endpoints, "/api-clients", AdminRoutes.ApiClientsTag);
        ApiClientListEndpoint.Map(clients);
        ApiClientRevokeEndpoint.Map(clients);
        ApiClientCreditEndpoint.Map(clients);
        DonationListEndpoint.Map(Admin(endpoints, "/donations", AdminRoutes.DonationsTag));
        MonitoringEndpoint.Map(Admin(endpoints, string.Empty, AdminRoutes.MonitoringTag));
        StorageEndpoint.Map(Admin(endpoints, string.Empty, AdminRoutes.StorageTag));
        BackupEndpoint.Map(Admin(endpoints, string.Empty, AdminRoutes.BackupsTag));
        var prerender = Admin(endpoints, "/prerender", AdminRoutes.PrerenderTag);
        PrerenderReportEndpoint.Map(prerender);
        PrerenderStartEndpoint.Map(prerender);
        PrerenderPassEndpoints.Map(prerender);
        PrerenderPatchesEndpoint.Map(prerender);
        PrerenderProgressEndpoint.Map(prerender);
        PrerenderPassVersionEndpoint.Map(prerender);
        PrerenderDemandEndpoint.Map(prerender);
        PrerenderRevisionEndpoint.Map(prerender);
        PrerenderSpeedEndpoints.Map(prerender);
        ScalewayBillEndpoint.Map(Admin(endpoints, "/scaleway", AdminRoutes.ScalewayTag));
        return endpoints;
    }

    private static IServiceCollection AddAdminReports(this IServiceCollection services)
    {
        // The web host adds the readiness checks; the command host gets a service that runs
        // none, so that every registration of the module resolves there too.
        services.AddHealthChecks();
        services.TryAddScoped<ServiceProbes>();
        services.TryAddScoped<DatabaseFigures>();
        services.TryAddScoped<VersionOverview>();
        // The cached reports outlive the request that assembles them: they open their own
        // scope for the readers above, never the one of the request.
        services.TryAddSingleton<MonitoringReporter>();
        services.TryAddSingleton<StorageReporter>();
        return services;
    }

    private static RouteGroupBuilder Admin(
        IEndpointRouteBuilder endpoints,
        string path,
        string tag) =>
        Group(endpoints, path, tag).RequireAuthorization(AuthorizationPolicies.Admin);

    // Reads personal data and acts on accounts: never cached, whatever sits in between.
    private static RouteGroupBuilder Group(
        IEndpointRouteBuilder endpoints,
        string path,
        string tag) =>
        endpoints.MapGroup(AdminRoutes.Prefix + path)
            .WithTags(tag)
            .AddEndpointFilter<NoStoreFilter>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);
}
