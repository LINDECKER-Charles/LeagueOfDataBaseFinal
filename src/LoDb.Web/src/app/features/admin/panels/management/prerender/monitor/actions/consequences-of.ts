import type { PrerenderPassView } from '../../../../../../../core/api/generated/models/prerender-pass-view';
import type { PrerenderReport } from '../../../../../../../core/api/generated/models/prerender-report';
import type { PrerenderUnitView } from '../../../../../../../core/api/generated/models/prerender-unit-view';
import { figure } from '../../../../../format/figure';
import type { AdminText } from '../../../../../shared/inject-admin-text';
import type { ConfirmData } from '../../dialogs/confirm-data';
import { decimalBytes } from '../../format/decimal-bytes';

/** An action of the monitor that asks first: a resume asks only after a pause of its own. */
export type ConsequenceKind = 'pause' | 'resume' | 'cancel';

/** What the consequences are counted from: the latest answers. */
export interface ConsequenceContext {
  readonly pass: PrerenderPassView;
  /** The units running now, from the progress. */
  readonly running: readonly PrerenderUnitView[];
  readonly report: PrerenderReport;
}

const CONFIRM = 'prerender.live.confirm';

function pauseOf(context: ConsequenceContext, texts: AdminText): ConfirmData {
  const { pass, running, report } = context;
  const unit = running[0];
  const lines = [
    unit
      ? texts.text(`${CONFIRM}.pause_current`, {
          version: unit.version,
          locale: unit.locale,
          done: figure(unit.cursor),
          total: figure(unit.pagesTotal),
        })
      : null,
    texts.text(`${CONFIRM}.pause_resume`),
    report.enqueuesPromotions ? texts.text(`${CONFIRM}.pause_promotions`) : null,
    texts.text(`${CONFIRM}.pause_slot`),
  ];
  return {
    title: texts.text(`${CONFIRM}.pause_title`, { id: pass.id }),
    lines: lines.filter((line) => line !== null),
    confirmLabel: texts.text(`${CONFIRM}.pause_confirm`),
    keepLabel: texts.text(`${CONFIRM}.pause_keep`),
    tone: 'gold',
  };
}

function resumeOf(context: ConsequenceContext, texts: AdminText): ConfirmData {
  return {
    title: texts.text(`${CONFIRM}.resume_title`),
    lines: [
      texts.text(`${CONFIRM}.resume_reason`, { reason: context.pass.pauseReason ?? '' }),
      texts.text(`${CONFIRM}.resume_warning`),
    ],
    confirmLabel: texts.text(`${CONFIRM}.resume_confirm`),
    keepLabel: texts.text(`${CONFIRM}.resume_keep`),
    tone: 'gold',
  };
}

// How much of its epoch the store holds, as its coverage says; null when not measured.
function coverageShare(context: ConsequenceContext): string | null {
  const { pass, report } = context;
  const epoch = report.storage.epochs.find((candidate) => candidate.epoch === pass.epoch);
  const coverage = epoch?.coverage;
  if (!coverage || coverage.expectedUnits === 0) {
    return null;
  }
  return figure(coverage.completeUnits / coverage.expectedUnits, 'share');
}

function cancelOf(context: ConsequenceContext, texts: AdminText): ConfirmData {
  const { pass } = context;
  const share = coverageShare(context);
  const lines = [
    texts.text(`${CONFIRM}.cancel_left`, { units: pass.units.pending }),
    texts.text(`${CONFIRM}.cancel_kept`, {
      pages: pass.pagesWritten,
      size: decimalBytes(pass.bytesWritten),
    }),
    share === null ? null : texts.text(`${CONFIRM}.cancel_partial`, { epoch: pass.epoch, share }),
    texts.text(`${CONFIRM}.cancel_later`, { epoch: pass.epoch }),
  ];
  return {
    title: texts.text(`${CONFIRM}.cancel_title`, { id: pass.id }),
    lines: lines.filter((line) => line !== null),
    confirmLabel: texts.text(`${CONFIRM}.cancel_confirm`, { id: pass.id }),
    keepLabel: texts.text(`${CONFIRM}.cancel_keep`),
    tone: 'danger',
  };
}

const BUILDERS: Readonly<
  Record<ConsequenceKind, (context: ConsequenceContext, texts: AdminText) => ConfirmData>
> = { pause: pauseOf, resume: resumeOf, cancel: cancelOf };

/**
 * What an action of the monitor will do, said before it acts (§ 4.7), with the figures of
 * the latest answers: a pause stops the unit running after its pages in flight, resumes
 * there and keeps the only active slot; a resume after a pause of its own warns it pauses
 * again without a fix; a cancel counts what will not be rendered, what stays stored and how
 * partial its epoch stays.
 */
export function consequencesOf(
  kind: ConsequenceKind,
  context: ConsequenceContext,
  texts: AdminText,
): ConfirmData {
  return BUILDERS[kind](context, texts);
}
