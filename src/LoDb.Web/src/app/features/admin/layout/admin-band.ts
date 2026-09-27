import { ChangeDetectionStrategy, Component, input } from '@angular/core';

/** What a band says: a failure (`alert`) or a success (`notice`). */
export type BandTone = 'alert' | 'notice';

/**
 * The legacy flash band, full width at the top of a block: red for what failed (a refused
 * action, a panel that could not load, unreadable figures), green for what an action did.
 * An alert is announced at once, a notice politely.
 */
@Component({
  selector: 'lodb-admin-band',
  template: '<ng-content />',
  styles: `
    :host {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      gap: 0.5rem;
      margin-block-end: 1.2rem;
      padding-block: 0.7rem;
      padding-inline: 0.9rem;
      border: 1px solid;
      font-size: 0.9rem;
      line-height: 1.55;
    }
    /* The legacy pale pink and pale green, mixed from the state and the parchment. */
    :host(.alert) {
      border-color: color-mix(in srgb, var(--color-bad) 40%, transparent);
      background: color-mix(in srgb, var(--color-bad) 10%, transparent);
      color: color-mix(in srgb, var(--color-bad) 30%, var(--color-gold-bright));
    }
    :host(.notice) {
      border-color: color-mix(in srgb, var(--color-good) 45%, transparent);
      background: color-mix(in srgb, var(--color-good) 10%, transparent);
      color: color-mix(in srgb, var(--color-good) 40%, var(--color-gold-bright));
    }
  `,
  host: {
    '[class]': 'tone()',
    '[attr.role]': "tone() === 'alert' ? 'alert' : 'status'",
  },
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AdminBand {
  readonly tone = input<BandTone>('alert');
}
