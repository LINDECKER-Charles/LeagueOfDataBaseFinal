import type { PrerenderPassView } from '../../../../../../../core/api/generated/models/prerender-pass-view';
import type { PrerenderReport } from '../../../../../../../core/api/generated/models/prerender-report';
import { figure } from '../../../../../format/figure';
import { stamp } from '../../../../../format/stamp';
import type { AdminText } from '../../../../../shared/inject-admin-text';
import type { StatusTile } from '../../../../../widgets/status-tabs/status-tile';

/** What the tile of the pass reads. */
export interface PassTileSource {
  readonly report: PrerenderReport;
  /** The pass ended within a day and not dismissed (`recentPassOf`); null otherwise. */
  readonly recent: PrerenderPassView | null;
  /** The share of the active pass the title shows, which never goes back; 0 without one. */
  readonly share: number;
}

const PASS = 'prerender.overview.views.pass';
const PAUSED = 'paused';
const COMPLETED = 'completed';

/** The state of an active pass as its tile says it: paused by itself calls for a hand. */
function activeState(pass: PrerenderPassView): Pick<StatusTile, 'tone'> & { key: string } {
  if (pass.state !== PAUSED) {
    return { key: 'running', tone: 'live' };
  }
  return pass.pauseReason ? { key: 'paused_itself', tone: 'bad' } : { key: 'paused', tone: 'warn' };
}

function activeFace(pass: PrerenderPassView, share: number, texts: AdminText) {
  const { key, tone } = activeState(pass);
  // Written as the monitor writes it, so that the tile and the monitor under it agree.
  return {
    value: figure(share, 'share'),
    sub: texts.text(`${PASS}.sub_${key}`, { id: pass.id, epoch: pass.epoch }),
    tone,
  };
}

function recentFace(pass: PrerenderPassView, texts: AdminText) {
  const isCompleted = pass.state === COMPLETED;
  return {
    value: texts.text(`${PASS}.value_${isCompleted ? 'completed' : 'cancelled'}`),
    sub: texts.text(`${PASS}.sub_ended`, { id: pass.id, date: stamp(pass.finishedAt, 'minute') }),
    tone: isCompleted ? ('good' as const) : ('muted' as const),
  };
}

function idleFace(report: PrerenderReport, texts: AdminText) {
  const sub = report.isConfigured
    ? texts.text(`${PASS}.sub_idle`, { promotions: report.enqueuesPromotions ? 'on' : 'off' })
    : texts.text(`${PASS}.sub_dormant`);
  return {
    value: texts.text(`${PASS}.value_idle`),
    sub,
    tone: report.isConfigured ? null : ('bad' as const),
  };
}

/**
 * The tile of the pass: the share of the active one, running (a live dot) or paused (gold,
 * red when it paused itself); the end of one within a day; otherwise none, and whether the
 * promotions open one, or that nothing can start on an unconfigured host.
 */
export function passTileOf(source: PassTileSource, texts: AdminText): Omit<StatusTile, 'key'> {
  const { report, recent, share } = source;
  const face = report.current
    ? activeFace(report.current, share, texts)
    : recent
      ? recentFace(recent, texts)
      : idleFace(report, texts);
  return { label: texts.text(`${PASS}.label`), ...face };
}
