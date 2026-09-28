import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { TranslocoPipe } from '@jsverse/transloco';

/**
 * The card every sign-in, registration and recovery page sits in, centred on its portal:
 * the eyebrow, the title of the page, then the projected form.
 */
@Component({
  selector: 'lodb-auth-card',
  imports: [TranslocoPipe],
  template: `<div class="auth-portal">
    <section
      class="hextech-frame hx-corners relative w-full max-w-md p-8 sm:p-10"
      aria-labelledby="auth-card-title"
      [class.text-center]="centered()"
    >
      <p class="eyebrow">{{ 'auth.eyebrow' | transloco }}</p>
      <h1
        id="auth-card-title"
        class="mt-2 font-beaufort text-3xl font-bold tracking-wide text-gold-grad uppercase"
      >
        {{ heading() }}
      </h1>
      <hr class="hx-rule mt-5" />
      <ng-content />
    </section>
  </div>`,
  styleUrl: './auth-card.css',
  host: { class: 'flex flex-1 flex-col' },
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AuthCard {
  /** The page's title, translated. */
  readonly heading = input.required<string>();
  /** Centres the card's texts: a page that only says what happens next, as a sent request. */
  readonly centered = input(false);
}
