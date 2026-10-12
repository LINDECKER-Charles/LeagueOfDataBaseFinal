import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { stamp } from '../../../format/stamp';
import { AdminTextPipe } from '../../../shared/admin-text-pipe';
import { injectAdminText } from '../../../shared/inject-admin-text';
import type { BackupNight, NightOutcome } from './backup-nights';

// Spelled out whole for the Tailwind scanner: a square per night, filled by its outcome.
const SQUARE = 'block size-[0.95rem] border';
const SQUARES: Readonly<Record<NightOutcome, string>> = {
  ok: `${SQUARE} border-good/70 bg-good/70`,
  recovered: `${SQUARE} border-gold/80 bg-gold/55`,
  failed: `${SQUARE} border-bad bg-bad/80`,
  none: `${SQUARE} border-gold/28 bg-transparent`,
};
const OUTCOMES: readonly NightOutcome[] = ['ok', 'recovered', 'failed', 'none'];

/**
 * The nights of the window at a glance, oldest first: a square each, green when every step
 * went through, gold when a retry was needed, red when a step ended failed, empty without a
 * pass. Each square names its day and its outcome, on hover and to a screen reader; a legend
 * follows.
 */
@Component({
  selector: 'lodb-backup-nights-strip',
  imports: [AdminTextPipe],
  template: `
    <ol class="flex flex-wrap gap-[0.3rem]">
      @for (night of labelled(); track night.day) {
        <li
          [class]="squares[night.outcome]"
          [attr.data-night]="night.day"
          [attr.data-outcome]="night.outcome"
          [title]="night.label"
        >
          <span class="sr-only">{{ night.label }}</span>
        </li>
      }
    </ol>
    <ul class="mt-4 flex flex-wrap gap-x-5 gap-y-2 text-[0.75rem] text-text-dim">
      @for (outcome of outcomes; track outcome) {
        <li class="flex items-center gap-2">
          <span [class]="squares[outcome]" aria-hidden="true"></span>
          {{ 'admin.backups.nights.outcomes.' + outcome | adminText }}
        </li>
      }
    </ul>
  `,
  host: { class: 'block' },
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class BackupNightsStrip {
  readonly nights = input.required<readonly BackupNight[]>();

  private readonly texts = injectAdminText();

  protected readonly squares = SQUARES;
  protected readonly outcomes = OUTCOMES;
  /** Each night with what it says: "12/10/2026 : toutes les étapes réussies (4 étapes)". */
  protected readonly labelled = computed(() =>
    this.nights().map((night) => ({
      ...night,
      label: this.texts.text('backups.nights.night', {
        day: stamp(`${night.day}T00:00:00Z`),
        outcome: this.texts.text(`backups.nights.outcomes.${night.outcome}`),
        steps: night.steps,
      }),
    })),
  );
}
