import { ChangeDetectionStrategy, Component, computed, input, output } from '@angular/core';
import type { PrerenderPassView } from '../../../../../../core/api/generated/models/prerender-pass-view';
import type { PrerenderReport } from '../../../../../../core/api/generated/models/prerender-report';
import { Button } from '../../../../../../ui/controls/button';
import { duration } from '../../../../format/duration';
import { figure } from '../../../../format/figure';
import { StampPipe } from '../../../../format/stamp-pipe';
import { AdminTextPipe } from '../../../../shared/admin-text-pipe';
import { injectAdminText } from '../../../../shared/inject-admin-text';
import { Badge, type Tone } from '../../../../widgets/badge';
import { foldRows } from '../../../../widgets/fold-rows';
import { FoldToggle } from '../../../../widgets/fold-toggle';
import { type BarSegment, StackedBar } from '../bars/stacked-bar';
import { scopeSummary } from '../common/scope/scope-summary';
import { DecimalBytesPipe } from '../format/decimal-bytes-pipe';
import { type ProgressFigures, progressSegments } from '../monitor/progress/progress-segments';
import { prerenderTone } from '../prerender-tone';

/** A row of the history, its texts ready. */
interface PassRow {
  readonly pass: PrerenderPassView;
  readonly isActive: boolean;
  readonly tone: Tone;
  readonly scope: string;
  readonly pages: string;
  readonly segments: readonly BarSegment[];
  readonly active: string;
}

// The latest passes show; the API sends 50 at most, folded under the toggle.
const SHOWN_PASSES = 10;
const PASSES = 'prerender.passes';
const SEGMENTS = 'prerender.live.segments';

/**
 * The « Historique » tab (§ 5.4): the latest passes, the active one first with a way back to
 * its monitor. Each says its epoch, how it ended, over what, how far it went (its figure is
 * its bar's, as in the monitor), its volume, running time, who launched it (a promotion or
 * the CLI has none), when it started and ended; its number opens its detail.
 */
@Component({
  selector: 'lodb-passes-tab',
  imports: [AdminTextPipe, Badge, Button, DecimalBytesPipe, FoldToggle, StackedBar, StampPipe],
  templateUrl: './passes-tab.html',
  host: { class: 'block' },
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PassesTab {
  readonly report = input.required<PrerenderReport>();
  /** A pass's detail, asked for. */
  readonly detail = output<number>();
  /** The way back to the monitor of the active pass. */
  readonly follow = output<void>();

  private readonly texts = injectAdminText();

  protected readonly rows = computed<PassRow[]>(() => {
    const { passes, current } = this.report();
    const others = passes.filter((pass) => pass.id !== current?.id);
    const ordered = current ? [current, ...others] : others;
    return ordered.map((pass) => this.rowOf(pass, pass.id === current?.id));
  });
  protected readonly fold = foldRows(this.rows, () => SHOWN_PASSES);

  // « Suivre » sits in a row that opens the detail: it goes to the monitor alone.
  protected followFrom(event: Event): void {
    event.stopPropagation();
    this.follow.emit();
  }

  private rowOf(pass: PrerenderPassView, isActive: boolean): PassRow {
    const figures = progressSegments(pass);
    return {
      pass,
      isActive,
      tone: prerenderTone(pass.state),
      scope: scopeSummary(pass.scope, this.texts),
      pages: this.pagesOf(figures),
      segments: figures.parts.map((part) => ({
        ...part,
        label: this.texts.text(
          part.key === 'completed' ? 'prerender.states.completed' : `${SEGMENTS}.${part.key}`,
        ),
      })),
      active: duration(pass.activeSeconds),
    };
  }

  // The figure of the bar beside it: processed over processed and left, « ≈ » while units
  // are to come; in units before any listed its pages.
  private pagesOf(figures: ProgressFigures): string {
    const { done, total, isEstimate, isUnits } = figures;
    const key = isUnits ? 'units' : isEstimate ? 'pages_estimated' : 'pages';
    return this.texts.text(`${PASSES}.${key}`, {
      done: figure(done),
      total: isUnits ? total : figure(total),
    });
  }
}
