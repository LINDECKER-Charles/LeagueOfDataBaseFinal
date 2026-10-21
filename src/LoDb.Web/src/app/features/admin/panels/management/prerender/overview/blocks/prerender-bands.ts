import { ChangeDetectionStrategy, Component, computed, input, output } from '@angular/core';
import { Button } from '../../../../../../../ui/controls/button';
import { CopyLink } from '../../../../../../../ui/controls/copy-link';
import { figure } from '../../../../../format/figure';
import { AdminBand } from '../../../../../layout/admin-band';
import { AdminTextPipe } from '../../../../../shared/admin-text-pipe';
import { injectAdminText } from '../../../../../shared/inject-admin-text';
import type { BandState } from '../state/band-state-of';

const COPY = 'prerender.common.copy';
// The variable nginx reads at its start (ADR 0011): its name never changes.
const SERVE_VARIABLE = 'LODB_ARCHIVE_SERVE_EPOCH';

/**
 * The bands under the head (§ 2.4): red while the host is not configured, with what to set;
 * green once an epoch is ready, with the variable to set, copiable, what the SSR keeps
 * serving and « Comment basculer ? »; green too for a standby epoch nearly whole, with
 * « Compléter l'epoch N ». Nothing switches from here: nginx reads the epoch at its start.
 */
@Component({
  selector: 'lodb-prerender-bands',
  imports: [AdminBand, AdminTextPipe, Button, CopyLink],
  template: `
    @let band = state();
    @if (band.isDormant) {
      <lodb-admin-band data-dormant>
        {{ 'admin.prerender.overview.bands.dormant' | adminText }}
      </lodb-admin-band>
    }
    @switch (band.epoch?.kind) {
      @case ('ready') {
        <lodb-admin-band tone="notice" data-ready-epoch>
          <span>{{ readyText() }}</span>
          <code class="font-mono text-gold-bright" data-variable>{{ variable() }}</code>
          <lodb-copy-link [url]="variable()" [labels]="copyLabels()" />
          @for (line of readyLines(); track line) {
            <span class="basis-full text-[0.85rem]" data-ready-ssr>{{ line }}</span>
          }
          <button lodbButton="ghost" lodbButtonSize="small" type="button" (click)="emitSwitch()">
            {{ 'admin.prerender.overview.bands.ready_how' | adminText }}
          </button>
        </lodb-admin-band>
      }
      @case ('nearly') {
        <lodb-admin-band tone="notice" data-nearly-epoch>
          <span>{{ nearlyText() }}</span>
          <button lodbButton lodbButtonSize="small" type="button" (click)="emitComplete()">
            {{ 'admin.prerender.overview.bands.nearly_cta' | adminText: { epoch: epoch() } }}
          </button>
        </lodb-admin-band>
      }
    }
  `,
  host: { class: 'block' },
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PrerenderBands {
  readonly state = input.required<BandState>();
  /** « Comment basculer ? » on the ready epoch. */
  readonly switchTo = output<number>();
  /** « Compléter l'epoch N » on the epoch nearly ready. */
  readonly complete = output<number>();

  private readonly texts = injectAdminText();

  protected readonly epoch = computed(() => this.state().epoch?.epoch ?? 0);
  protected readonly variable = computed(() => `${SERVE_VARIABLE}=${this.epoch()}`);
  protected readonly copyLabels = computed(() => ({
    copy: this.texts.text(`${COPY}.copy`),
    copied: this.texts.text(`${COPY}.copied`),
    error: this.texts.text(`${COPY}.error`),
  }));
  protected readonly readyText = computed(() => {
    const band = this.state().epoch;
    return band?.kind === 'ready'
      ? this.texts.text('prerender.overview.bands.ready', {
          epoch: band.epoch,
          versions: band.versions,
        })
      : '';
  });
  // What the SSR keeps serving once switched: failures for good, then failed pages.
  protected readonly readyLines = computed(() => {
    const band = this.state().epoch;
    if (band?.kind !== 'ready') {
      return [];
    }
    const lines: string[] = [];
    if (band.failedUnits > 0) {
      lines.push(this.bandText('ready_failed_units', { units: band.failedUnits }));
    }
    if (band.failedPages > 0) {
      lines.push(this.bandText('ready_failed_pages', { pages: band.failedPages }));
    }
    return lines;
  });
  protected readonly nearlyText = computed(() => {
    const band = this.state().epoch;
    return band?.kind === 'nearly'
      ? this.bandText('nearly', {
          epoch: band.epoch,
          share: figure(band.share, 'share'),
          missing: band.missing,
        })
      : '';
  });

  protected emitSwitch(): void {
    this.switchTo.emit(this.epoch());
  }

  protected emitComplete(): void {
    this.complete.emit(this.epoch());
  }

  private bandText(key: string, params: Readonly<Record<string, unknown>>): string {
    return this.texts.text(`prerender.overview.bands.${key}`, params);
  }
}
