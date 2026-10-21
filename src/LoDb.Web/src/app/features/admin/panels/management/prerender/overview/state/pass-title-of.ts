import type { PrerenderReport } from '../../../../../../../core/api/generated/models/prerender-report';
import { figure } from '../../../../../format/figure';
import { progressSegments } from '../../monitor/progress/progress-segments';
import { recentPassOf } from './recent-pass-of';

/** The share a title showed for a pass: it never goes back for that pass. */
export interface TitleShare {
  readonly passId: number;
  /** From 0 to 1. */
  readonly share: number;
}

/** The title of the document, as an `overview.doc_title.*` key and its parameters. */
export interface PassTitle {
  readonly key: string;
  readonly params: Readonly<Record<string, unknown>>;
  /** The share shown, to pass back on the next report; null without an active pass. */
  readonly held: TitleShare | null;
}

const TITLE = 'prerender.overview.doc_title';
const PAUSED = 'paused';

/**
 * The title of the document (§ 2.5), which the operator reads in a background tab for the
 * days a pass lasts: the share of an active pass, paused or not, as the monitor reckons it
 * (never back for the same pass, `held` being the last one shown), a pass ended within a day
 * and not dismissed, or the page's name alone.
 */
export function passTitleOf(
  report: PrerenderReport | undefined,
  dismissed: number | null,
  held: TitleShare | null,
): PassTitle {
  const current = report?.current;
  if (current) {
    const read = progressSegments(current).share;
    const share = held?.passId === current.id ? Math.max(held.share, read) : read;
    return {
      key: `${TITLE}.${current.state === PAUSED ? 'paused' : 'running'}`,
      params: { share: figure(share, 'percent') },
      held: { passId: current.id, share },
    };
  }
  const recent = report ? recentPassOf(report, dismissed) : null;
  return recent
    ? { key: `${TITLE}.${recent.state}`, params: { id: recent.id }, held: null }
    : { key: `${TITLE}.idle`, params: {}, held: null };
}
