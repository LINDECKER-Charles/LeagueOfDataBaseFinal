using LoDb.Api.Hosting;
using LoDb.Api.Modules.Admin.ApiClients;
using LoDb.Api.Modules.Admin.Builds;
using LoDb.Api.Modules.Admin.Contacts;
using LoDb.Api.Modules.Admin.Donations;
using LoDb.Api.Modules.Admin.Http;
using LoDb.Api.Modules.Admin.Mfa;
using LoDb.Api.Modules.Admin.Monitoring;
using LoDb.Api.Modules.Admin.Storage;
using LoDb.Api.Modules.Admin.Users;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace LoDb.Api.Modules.Admin;

/// <summary>
/// Admin module: the second-factor enrollment of the administrators, the moderation of
/// accounts, builds, contact messages and API keys, the donations, the monitoring and the
/// storage report.
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
        return endpoints;
    }

    private static IServiceCollection AddAdminReports(this IServiceCollection services)
    {
        services.TryAddScoped<ServiceProbes>();
        services.TryAddScoped<DatabaseFigures>();
        services.TryAddScoped<VersionOverview>();
        services.TryAddScoped<MonitoringReporter>();
        services.TryAddScoped<StorageReporter>();
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
