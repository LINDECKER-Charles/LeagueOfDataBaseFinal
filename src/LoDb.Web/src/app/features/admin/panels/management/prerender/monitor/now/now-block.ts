import { ChangeDetectionStrategy, Component, computed, input, output } from '@angular/core';
import type { PrerenderPassProgress } from '../../../../../../../core/api/generated/models/prerender-pass-progress';
import type { PrerenderUnitKey } from '../../../../../../../core/api/generated/models/prerender-unit-key';
import type { PrerenderUnitView } from '../../../../../../../core/api/generated/models/prerender-unit-view';
import type { PrerenderWaitView } from '../../../../../../../core/api/generated/models/prerender-wait-view';
import { Button } from '../../../../../../../ui/controls/button';
import { duration } from '../../../../../format/duration';
import { figure } from '../../../../../format/figure';
import { stamp } from '../../../../../format/stamp';
import { AdminTextPipe } from '../../../../../shared/admin-text-pipe';
import { injectAdminText } from '../../../../../shared/inject-admin-text';
import { type BarSegment, StackedBar } from '../../bars/stacked-bar';
import { localeName } from '../../common/patches/locale-name';
import { relativeTime } from '../../live/badge/relative-time';

/** A unit running, or stopped by a pause, as the block writes it. */
interface NowUnit {
  readonly unit: PrerenderUnitView;
  readonly line: string;
  readonly pages: string;
  readonly since: string | null;
  readonly segments: readonly BarSegment[];
}

const NOW = 'prerender.live.now';
const PAUSED = 'paused';
const SECOND_MS = 1_000;
// « 21/10/2026 09:12 »: the hour alone, the day being today's.
const HOUR_LENGTH = 5;

/**
 * « En ce moment » (§ 4.5): the unit running, its own bar, how far and since when, its last
 * page; the next units; the versions that wait and until when; a SSR out of reach; the
 * signals of the background task this instance cannot see, or a task turned off. Paused, it
 * reads « Reprendra par »: the unit a pause stopped, where it stopped, without a « depuis »;
 * the waits and the SSR, which a paused pass does not meet, are left out.
 */
@Component({
  selector: 'lodb-now-block',
  imports: [AdminTextPipe, Button, StackedBar],
  templateUrl: './now-block.html',
  styles: `
    .now-band {
      margin: 0;
      padding-block: 0.45rem;
      padding-inline: 0.7rem;
      border: 1px solid;
      font-size: 0.82rem;
    }
    .now-band--gold {
      border-color: color-mix(in srgb, var(--color-gold) 45%, transparent);
      background: color-mix(in srgb, var(--color-gold) 10%, transparent);
      color: var(--color-gold-bright);
    }
    .now-band--bad {
      border-color: color-mix(in srgb, var(--color-bad) 40%, transparent);
      background: color-mix(in srgb, var(--color-bad) 10%, transparent);
      color: color-mix(in srgb, var(--color-bad) 30%, var(--color-gold-bright));
    }
    .now-chip {
      padding-block: 0.1rem;
      padding-inline: 0.45rem;
      border: 1px solid color-mix(in srgb, var(--color-gold) 28%, transparent);
      color: var(--color-text-muted);
    }
    .now-chip--waiting {
      border-style: dashed;
      color: var(--color-text-dim);
    }
  `,
  host: { class: 'flex min-w-0 flex-col gap-3' },
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class NowBlock {
  readonly progress = input.required<PrerenderPassProgress>();
  /** The server's clock, in milliseconds. */
  readonly serverNow = input.required<number>();
  /** A version's detail, asked for. */
  readonly opened = output<string>();

  private readonly texts = injectAdminText();

  protected readonly isPaused = computed(() => this.progress().pass.state === PAUSED);
  protected readonly units = computed(() =>
    this.progress().running.map((unit) => this.nowOf(unit)),
  );
  protected readonly next = computed(() => {
    const { next, pulse } = this.progress();
    // The server leaves the waiting versions out only when it sees them itself.
    const waiting = pulse.observedAt ? new Set<string>() : this.waitingVersions();
    return next.map((key) => ({ key, isWaiting: waiting.has(key.version) }));
  });
  protected readonly waits = computed(() =>
    this.isPaused() ? [] : this.progress().pulse.waiting.map((wait) => this.waitLine(wait)),
  );
  protected readonly ssrDown = computed(() => {
    const since = this.progress().pulse.ssrUnreachableSince;
    return since && !this.isPaused()
      ? this.texts.text(`${NOW}.ssr_down`, { duration: this.since(since) })
      : null;
  });
  protected readonly isRemote = computed(
    () => !this.isPaused() && !this.progress().pulse.observedAt,
  );
  protected readonly isWorkerOff = computed(() => !this.progress().pulse.isWorkerEnabled);

  protected keyLabel(key: PrerenderUnitKey): string {
    return `${key.version} · ${key.locale}`;
  }

  private waitingVersions(): ReadonlySet<string> {
    return new Set(this.progress().pulse.waiting.map((wait) => wait.version));
  }

  private nowOf(unit: PrerenderUnitView): NowUnit {
    const processed = unit.pagesWritten + unit.pagesSkipped + unit.pagesFailed;
    const share = figure(unit.pagesTotal > 0 ? processed / unit.pagesTotal : 0, 'percent');
    const names = { version: unit.version, locale: unit.locale, language: localeName(unit.locale) };
    const done = figure(processed);
    const line = this.isPaused()
      ? this.texts.text(`${NOW}.stopped`, { ...names, done, total: figure(unit.pagesTotal), share })
      : this.texts.text(`${NOW}.unit`, names);
    return {
      unit,
      line,
      pages: this.texts.text(`${NOW}.pages`, { done, total: unit.pagesTotal, share }),
      since: this.isPaused() ? null : this.sinceLine(unit),
      segments: this.segmentsOf(unit, processed),
    };
  }

  private sinceLine(unit: PrerenderUnitView): string {
    return this.texts.text(`${NOW}.since`, {
      duration: this.since(unit.startedAt ?? unit.updatedAt),
      ago: relativeTime(Date.parse(unit.updatedAt), this.serverNow(), this.texts),
    });
  }

  private segmentsOf(unit: PrerenderUnitView, processed: number): BarSegment[] {
    const segment = (key: string, value: number, color: string): BarSegment => ({
      key,
      value,
      color,
      label: this.texts.text(`prerender.live.segments.${key}`),
    });
    return [
      segment('written', unit.pagesWritten, 'var(--color-good)'),
      segment(
        'skipped',
        unit.pagesSkipped,
        'color-mix(in srgb, var(--color-gold) 70%, transparent)',
      ),
      segment('failed', unit.pagesFailed, 'var(--color-bad)'),
      segment('left', Math.max(0, unit.pagesTotal - processed), 'var(--color-track)'),
    ];
  }

  private waitLine(wait: PrerenderWaitView): string {
    return this.texts.text(`${NOW}.wait`, {
      version: wait.version,
      reason: this.texts.text(`${NOW}.reasons.${wait.reason}`),
      duration: this.since(wait.since),
      at: stamp(wait.givesUpAt, 'minute').slice(-HOUR_LENGTH),
    });
  }

  private since(iso: string): string {
    return duration((this.serverNow() - Date.parse(iso)) / SECOND_MS);
  }
}
