namespace LoDb.Api.Modules.Admin.ApiClients.Views;

/// <summary>How many active keys hold a plan.</summary>
internal sealed record PlanCount
{
    public required string Plan { get; init; }

    public required int Keys { get; init; }
}
