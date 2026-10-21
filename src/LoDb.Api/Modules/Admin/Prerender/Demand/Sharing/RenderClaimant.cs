namespace LoDb.Api.Modules.Admin.Prerender.Demand.Sharing;

/// <summary>Who asks the pass's SSR for a render: a manual pass, or the automatic queue.</summary>
internal enum RenderClaimant
{
    Pass,
    Queue,
}
