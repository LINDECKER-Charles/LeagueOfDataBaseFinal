import { ChangeDetectionStrategy, Component, input } from '@angular/core';

/**
 * A labelled separator between the sections of a panel, the legacy `.rule[data-label]`: a
 * gold-deep line fading at both ends, a hollow diamond in its middle, the label at its start
 * on the void. Not the site's `hx-rule`, whose filled glowing diamond the admin never had.
 */
@Component({
  selector: 'lodb-admin-rule',
  template: `<span class="rule-label">{{ label() }}</span>`,
  styles: `
    :host {
      position: relative;
      display: block;
      block-size: 1px;
      margin-block: 2rem 1.5rem;
      background: linear-gradient(90deg, transparent, var(--color-gold-deep), transparent);
    }
    /* The middle of the line is the same point whatever the direction: left is right here. */
    :host::after {
      content: '';
      position: absolute;
      left: 50%;
      inset-block-start: 50%;
      inline-size: 8px;
      block-size: 8px;
      translate: -50% -50%;
      rotate: 45deg;
      background: var(--color-void);
      border: 1px solid var(--color-gold);
    }
    .rule-label {
      position: absolute;
      inset-inline-start: 0;
      inset-block-start: 50%;
      translate: 0 -50%;
      padding-inline-end: 0.9rem;
      background: var(--color-void);
      font-family: var(--font-beaufort);
      font-size: 0.72rem;
      line-height: 1.55;
      letter-spacing: 0.2em;
      text-transform: uppercase;
      color: var(--color-gold);
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AdminRule {
  /** The name of the section it opens, translated. */
  readonly label = input.required<string>();
}
