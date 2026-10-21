import type { PrerenderPassView } from '../../../../../../../core/api/generated/models/prerender-pass-view';
import type { PrerenderReport } from '../../../../../../../core/api/generated/models/prerender-report';

const ENDED = ['completed', 'cancelled'];
// A pass that ended within a day still has something to say: its recap, its title.
const RECENT_MS = 24 * 60 * 60 * 1_000;

/**
 * The pass that ended recently (§ 2.4, § 4.8): the newest of the report, completed or
 * cancelled less than 24 hours before the report was generated (the server's clock, not the
 * browser's), and not dismissed by this visitor. Null while a pass is active.
 */
export function recentPassOf(
  report: PrerenderReport,
  dismissed: number | null,
): PrerenderPassView | null {
  const [newest] = report.passes;
  if (report.current || !newest?.finishedAt || !ENDED.includes(newest.state)) {
    return null;
  }
  const age = Date.parse(report.generatedAt) - Date.parse(newest.finishedAt);
  return age < RECENT_MS && newest.id !== dismissed ? newest : null;
}
