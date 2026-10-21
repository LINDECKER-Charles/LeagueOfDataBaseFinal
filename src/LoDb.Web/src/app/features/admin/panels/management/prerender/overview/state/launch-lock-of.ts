import type { PrerenderReport } from '../../../../../../../core/api/generated/models/prerender-report';

/**
 * Why no pass may be launched now (§ 2.4), each an `overview.cta.locked_*` key: the state of
 * the pre-rendering is not read yet, the host is not configured, or a pass is active (one at
 * a time), which the page names. A lock is also the parameters of its text: `{ id }`.
 */
export type LaunchLock =
  | { readonly kind: 'loading' }
  | { readonly kind: 'dormant' }
  | { readonly kind: 'active'; readonly id: number };

/** Why no pass may be launched from `report`, null when one may. */
export function launchLockOf(report: PrerenderReport | undefined): LaunchLock | null {
  if (!report) {
    return { kind: 'loading' };
  }
  if (!report.isConfigured) {
    return { kind: 'dormant' };
  }
  return report.current ? { kind: 'active', id: report.current.id } : null;
}
