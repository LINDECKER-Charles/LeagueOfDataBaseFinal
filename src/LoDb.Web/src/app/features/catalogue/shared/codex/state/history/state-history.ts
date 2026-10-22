import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import type { StateNextKind } from '../../../../../../core/api/generated/models/state-next-kind';
import type { PageContext } from '../../../../../../core/context/page-context';
import { injectTranslate } from '../../texts/inject-translate';
import type { SubjectNames } from '../labels/subject-names';
import { injectStateLink } from '../links/inject-state-link';
import type { StatefulDetails } from '../stateful-details';
import { historyRowsOf } from './history-rows-of';

/** What followed a closed state, for each way it ended. */
const NEXT_KEYS: Readonly<Record<StateNextKind, string>> = {
  changed: 'detail.history.changed_at',
  removed: 'detail.history.removed_at',
};

/**
 * « Historique »: the entity's states up to the page's, newest first, each linking its
 * canonical with its run of patches and the fields it changed; on a page of a closed state,
 * first, the patch that changed the entity again (by its versioned URL) or retired it. Links
 * only, from the API's answer: no catalogue of another version is loaded to render them.
 */
@Component({
  selector: 'lodb-state-history',
  imports: [RouterLink],
  template: `<section aria-labelledby="state-history">
    <header class="codex-header mb-5">
      <h2 id="state-history">{{ title() }}</h2>
    </header>
    <ol class="grid gap-3">
      @if (next(); as next) {
        <li class="text-sm text-text-muted">
          @if (next.url; as url) {
            <a class="hx-chip nav-link px-3 py-1.5 hover:text-hex" [routerLink]="url">{{
              next.text
            }}</a>
          } @else {
            <span class="hx-chip px-3 py-1.5">{{ next.text }}</span>
          }
        </li>
      }
      @for (row of rows(); track row.range) {
        <li class="flex flex-wrap items-center gap-x-3 gap-y-1.5">
          <a
            class="hx-chip nav-link px-3 py-1.5 hover:text-hex"
            [class.text-hex]="row.isPage"
            [routerLink]="row.url"
            >{{ row.range }}</a
          >
          @if (row.kind; as kind) {
            <span class="hx-chip-hex">{{ kind }}</span>
          }
          @if (row.fields) {
            <span class="text-sm text-text-muted">{{ row.fields }}</span>
          }
        </li>
      }
    </ol>
  </section>`,
  host: { class: 'block' },
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class StateHistory {
  readonly context = input.required<PageContext>();
  readonly details = input.required<StatefulDetails>();
  /** The page's own names of the fields' subjects, which the history leaves unnamed. */
  readonly names = input<SubjectNames>();

  private readonly translate = injectTranslate();
  private readonly linkTo = injectStateLink();
  protected readonly title = computed(() => this.translate()('detail.history.title'));
  protected readonly rows = computed(() => {
    const { history, state } = this.details();
    const format = { locale: this.context().locale, translate: this.translate() };
    return historyRowsOf(history, state, { ...format, names: this.names() }).map((row) => ({
      ...row,
      url: this.linkTo(this.context(), row.link),
    }));
  });
  protected readonly next = computed(() => {
    const next = this.details().next;
    if (next === null) {
      return null;
    }
    const text = this.translate()(NEXT_KEYS[next.kind], { version: next.version });
    return { text, url: next.link === null ? null : this.linkTo(this.context(), next.link) };
  });
}
