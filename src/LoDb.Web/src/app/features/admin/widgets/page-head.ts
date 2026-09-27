import { ChangeDetectionStrategy, Component, effect, inject, input } from '@angular/core';
import type { Locale } from '../../../core/i18n/locales';
import { Seo } from '../../../core/seo/seo';

// The admin speaks French whatever the locale of the site.
const ADMIN_LOCALE: Locale = 'fr';

/**
 * The head of an admin page: its eyebrow, its `h1` and what it shows, the page's actions at
 * the end. It also titles the document "{title} · Admin · LODB", never indexed.
 */
@Component({
  selector: 'lodb-page-head',
  template: `
    <div class="mb-6 flex flex-wrap items-end justify-between gap-4">
      <div class="min-w-0">
        <p class="eyebrow">{{ eyebrow() }}</p>
        <h1 class="mt-1 font-beaufort text-3xl text-gold-grad">{{ title() }}</h1>
        @if (subtitle()) {
          <p class="mt-1.5 max-w-3xl text-sm text-text-muted">{{ subtitle() }}</p>
        }
      </div>
      <div class="flex flex-wrap items-center gap-2">
        <ng-content />
      </div>
    </div>
  `,
  host: { class: 'block' },
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PageHead {
  /** The section of the admin, translated. */
  readonly eyebrow = input.required<string>();
  /** The page's title, translated. */
  readonly title = input.required<string>();
  readonly subtitle = input('');

  private readonly seo = inject(Seo);

  constructor() {
    effect(() => {
      const title = this.title();
      // The pipe answers nothing until the catalogue arrives: no title before it.
      if (title !== '') {
        void this.seo.apply({
          kind: 'private',
          titleFormat: 'admin',
          locale: ADMIN_LOCALE,
          title,
        });
      }
    });
  }
}
