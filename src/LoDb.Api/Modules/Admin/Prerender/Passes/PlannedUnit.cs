using LoDb.Domain.Languages;
using LoDb.Domain.Versions;

namespace LoDb.Api.Modules.Admin.Prerender.Passes;

/// <summary>A (version, locale) a pass will render, and its rank: lower goes first.</summary>
internal sealed record PlannedUnit(PatchVersion Version, UiLocale Locale, int Priority);
