using LoDb.Api.Modules.Admin.Prerender.Pages;

namespace LoDb.Api.Modules.Admin.Prerender.Passes;

/// <summary>The pass the worker writes: its row, the epoch it writes and its perimeter.</summary>
internal sealed record RunningPass(long Id, int Epoch, PassScope Scope);
