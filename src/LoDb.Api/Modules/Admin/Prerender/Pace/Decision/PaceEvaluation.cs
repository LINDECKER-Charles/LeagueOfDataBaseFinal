using LoDb.Api.Modules.Admin.Prerender.Pace.Signals;

namespace LoDb.Api.Modules.Admin.Prerender.Pace.Decision;

/// <summary>
/// One evaluation of the signals: their board, when, and the renders the pass started over
/// the last minute.
/// </summary>
internal sealed record PaceEvaluation(PaceBoard Board, DateTimeOffset At, int AchievedStarts);
