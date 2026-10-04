import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { injectAdminTitle } from './admin-title';

/**
 * The head of an admin page, the legacy `page_head` in its toolbar: its eyebrow, its `h1`
 * and what it shows, the page's actions at the end, centred on it. It also titles the
 * document "{documentTitle} · Admin · LODB", never indexed; the document title is the `h1`
 * unless the legacy one was shorter ("Messages" for "Messages de contact").
 */
@Component({
  selector: 'lodb-page-head',
  template: `
    <div class="flex flex-wrap items-center justify-between gap-2.5">
      <!-- The legacy head kept its margin inside the toolbar: the actions centre on both. -->
      <div class="mb-7 min-w-0">
        <p class="eyebrow">{{ eyebrow() }}</p>
        <h1 class="mt-1.5 font-beaufort text-[1.7rem] font-bold tracking-[0.06em] text-gold-bright">
          {{ title() }}
        </h1>
        @if (subtitle()) {
          <p class="mt-1.5 max-w-[46rem] text-base text-text-muted">{{ subtitle() }}</p>
        }
      </div>
      <div class="page-actions flex flex-wrap items-center gap-3">
        <ng-content />
      </div>
    </div>
  `,
  // A head with actions sat in the legacy `.page-toolbar`, which added its own margin.
  host: { class: 'block has-[.page-actions>*]:mb-6' },
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PageHead {
  /** The section of the admin, translated. */
  readonly eyebrow = input.required<string>();
  /** The page's title, translated. */
  readonly title = input.required<string>();
  readonly subtitle = input('');
  /** The title of the document, translated; the `h1` when left out. */
  readonly documentTitle = input('');

  constructor() {
    injectAdminTitle(() => this.documentTitle() || this.title());
  }
}
