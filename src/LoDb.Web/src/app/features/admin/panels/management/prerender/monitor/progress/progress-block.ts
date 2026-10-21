import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { Sparkline } from '../../../../../charts/sparkline';
import { duration } from '../../../../../format/duration';
import { figure } from '../../../../../format/figure';
import { stamp } from '../../../../../format/stamp';
import { AdminTextPipe } from '../../../../../shared/admin-text-pipe';
import { injectAdminText } from '../../../../../shared/inject-admin-text';
import { Legend, type LegendItem } from '../../../../../widgets/legend';
import { type BarSegment, StackedBar } from '../../bars/stacked-bar';
import { decimalBytes } from '../../format/decimal-bytes';
import type { MonitorState } from '../merge-progress';
import { progressSegments } from './progress-segments';

const LIVE = 'prerender.live';
const PAUSED = 'paused';
const SECOND_MS = 1_000;
const NOTE_ID = 'prerender-progress-note';
// The keys of the segments counted in units, which `live.segments` does not name.
const UNIT_LABELS: Readonly<Record<string, string>> = {
  completed: 'prerender.states.completed',
  failed: 'prerender.states.failed',
};

/**
 * How far the pass is (§ 4.2, § 4.3): the share on screen, which never goes back, the pages
 * done over the total, estimated (« ≈ ») while units are to come, the bar of pages written,
 * skipped, failed and left with its legend, the units; then four readouts: the pace now and
 * on average with the curve of the last 15 minutes, the end at the mean pace and, when they
 * differ, at the current one, the running time and the volume written. Paused, the bar is
 * desaturated, the pace stopped and the end on hold.
 */
@Component({
  selector: 'lodb-progress-block',
  imports: [AdminTextPipe, Legend, Sparkline, StackedBar],
  templateUrl: './progress-block.html',
  styles: `
    .progress-share {
      font-family: var(--font-beaufort);
      font-size: 2.6rem;
      line-height: 1;
      color: var(--color-gold-bright);
      font-variant-numeric: tabular-nums;
    }
    .readouts {
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(12rem, 1fr));
      gap: 1rem 1.5rem;
    }
    .pace-curve {
      display: inline-block;
      inline-size: 5rem;
      block-size: 1.25rem;
      vertical-align: middle;
    }
    @media (max-width: 639.98px) {
      .progress-share {
        font-size: 2rem;
      }
      .readouts {
        grid-template-columns: minmax(0, 1fr);
        gap: 0.6rem;
      }
      .readout {
        display: grid;
        grid-template-columns: 7rem minmax(0, 1fr);
        column-gap: 0.75rem;
      }
      .readout > :not(:first-child) {
        grid-column: 2;
      }
    }
  `,
  host: { class: 'flex flex-col gap-4' },
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ProgressBlock {
  readonly state = input.required<MonitorState>();
  /** The server's clock, in milliseconds. */
  readonly serverNow = input.required<number>();

  private readonly texts = injectAdminText();

  protected readonly noteId = NOTE_ID;
  protected readonly pass = computed(() => this.state().progress.pass);
  protected readonly isPaused = computed(() => this.pass().state === PAUSED);
  protected readonly figures = computed(() =>
    progressSegments(this.pass(), this.state().shownShare),
  );
  protected readonly shown = computed(() => figure(this.state().shownShare, 'share'));
  protected readonly segments = computed<BarSegment[]>(() =>
    this.figures().parts.map((part) => ({ ...part, label: this.labelOf(part.key) })),
  );
  protected readonly legend = computed<LegendItem[]>(() =>
    this.segments().map((segment) => ({
      label: segment.label,
      color: segment.color,
      value:
        (segment.key === 'left' && this.figures().isEstimate ? '≈ ' : '') + figure(segment.value),
    })),
  );
  protected readonly pages = computed(() => {
    const { done, total, isEstimate, isUnits } = this.figures();
    if (isUnits) {
      return this.texts.text(`${LIVE}.first_unit`);
    }
    const key = isEstimate ? 'pages_estimated' : 'pages_exact';
    return this.texts.text(`${LIVE}.${key}`, { done: figure(done), total });
  });
  protected readonly barLabel = computed(() => {
    const pass = this.pass();
    const left = this.figures().parts.find((part) => part.key === 'left')?.value ?? 0;
    return this.texts.text(`${LIVE}.progress_label`, {
      share: this.shown(),
      written: pass.pagesWritten,
      skipped: figure(pass.pagesSkipped),
      failed: figure(pass.pagesFailed),
      left: figure(left),
    });
  });
  // Paused, the unit left running in the base is stopped: « 1 en pause », not « en cours ».
  protected readonly units = computed(() => {
    const { completed, total, running, pending, failed } = this.pass().units;
    return this.texts.text(`${LIVE}.${this.isPaused() ? 'units_paused' : 'units'}`, {
      completed: figure(completed),
      total: figure(total),
      running: figure(running),
      pending: figure(pending),
      failed: figure(failed),
    });
  });
  protected readonly pace = computed(() => this.paceOf());
  protected readonly curve = computed(() => this.curveOf());
  protected readonly eta = computed(() => this.etaOf());
  protected readonly active = computed(() => duration(this.pass().activeSeconds));
  protected readonly volume = computed(() => decimalBytes(this.pass().bytesWritten));
  protected readonly perPage = computed(() => {
    const { pagesWritten, bytesWritten } = this.pass();
    return pagesWritten > 0 ? decimalBytes(bytesWritten / pagesWritten) : null;
  });

  private labelOf(key: string): string {
    const unitKey = this.figures().isUnits ? UNIT_LABELS[key] : undefined;
    return this.texts.text(unitKey ?? `${LIVE}.segments.${key}`);
  }

  // The instant pace in large, else the mean one; the mean one under it, and « mesure en
  // cours… » while the samples span less than 20 seconds.
  private paceOf(): { value: string; lines: string[] } {
    const average = this.pass().pagesPerMinute ?? null;
    if (this.isPaused()) {
      return { value: this.texts.text(`${LIVE}.pace.stopped`), lines: [] };
    }
    const samples = this.state().samples;
    const shown = samples.instant() ?? average;
    const value =
      shown === null ? '—' : this.texts.text(`${LIVE}.pace.value`, { pace: Math.round(shown) });
    const lines = [
      average === null
        ? null
        : this.texts.text(`${LIVE}.pace.average`, { pace: Math.round(average) }),
      samples.isMeasuring() ? this.texts.text(`${LIVE}.pace.measuring`) : null,
    ];
    return { value, lines: lines.filter((line) => line !== null) };
  }

  // The curve's values, broken where a hole of more than two minutes left nothing seen.
  private curveOf(): { values: number[]; gaps: number[] } {
    const values: number[] = [];
    const gaps: number[] = [];
    let isBroken = false;
    for (const rate of this.state().samples.rates()) {
      if (rate === null) {
        isBroken = values.length > 0;
        continue;
      }
      if (isBroken) {
        gaps.push(values.length);
        isBroken = false;
      }
      values.push(rate);
    }
    return { values, gaps };
  }

  private etaOf(): { value: string; lines: string[] } {
    const eta = this.state().eta;
    if (eta.state !== 'known') {
      return { value: '—', lines: [this.texts.text(`${LIVE}.eta.${eta.state}`)] };
    }
    const main = eta.average ?? eta.current ?? 0;
    const lines: string[] = [];
    if (eta.average !== null) {
      lines.push(this.texts.text(`${LIVE}.eta.average`, { in: this.inOf(eta.average) }));
    }
    if (eta.current !== null && (eta.isCurrentShown || eta.average === null)) {
      lines.push(this.texts.text(`${LIVE}.eta.now`, { in: this.inOf(eta.current) }));
    }
    return { value: stamp(new Date(main).toISOString(), 'minute'), lines };
  }

  private inOf(at: number): string {
    const length = duration((at - this.serverNow()) / SECOND_MS);
    return this.texts.text('prerender.common.time.in', { duration: length });
  }
}
