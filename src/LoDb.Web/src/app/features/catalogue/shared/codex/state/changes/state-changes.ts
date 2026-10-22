import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import type { EntityChanges } from '../../../../../../core/api/generated/models/entity-changes';
import type { Locale } from '../../../../../../core/i18n/locales';
import { injectTranslate } from '../../texts/inject-translate';
import type { SubjectNames } from '../labels/subject-names';
import { changeRowsOf } from './change-rows-of';

/** The block's heading for each way a state opens; a baseline one has no block. */
const TITLE_KEYS: Readonly<Record<Exclude<EntityChanges['kind'], 'baseline'>, string>> = {
  changed: 'detail.changes.title',
  new: 'detail.changes.new',
  returned: 'detail.changes.returned',
};

/**
 * « Ce qui a changé dans ce patch » on the page of a state's opener: each field the patch
 * changed, before then after, a text by its diff written as text (`<del>`, `<ins>`, never
 * HTML); « Nouveau dans ce patch » for a new entity, « De retour » for a return, compared
 * with its state before the absence. Rendered by the server, read by crawlers: the pieces of
 * a value are spaced, and its arrow is read, so that the text says « 3,000 → 3,100 ».
 */
@Component({
  selector: 'lodb-state-changes',
  template: `<section aria-labelledby="state-changes">
    <header class="codex-header mb-5">
      <h2 id="state-changes">{{ title() }}</h2>
    </header>
    @if (compared(); as compared) {
      <p class="mb-5 text-sm text-text-muted">{{ compared }}</p>
    }
    @if (rows().length > 0) {
      <dl class="grid gap-4">
        @for (row of rows(); track $index) {
          <div class="border-s-2 border-hex/40 ps-4">
            <dt class="mb-1 text-sm font-semibold text-gold-light">{{ row.label }}</dt>
            <dd class="text-sm text-text" [class.whitespace-pre-line]="row.isText">
              @for (piece of row.pieces; track $index) {
                @switch (piece.kind) {
                  @case ('removed') {
                    <del class="text-danger-light">{{ piece.text }}</del>
                  }
                  @case ('added') {
                    <ins class="bg-hex/15 text-hex-bright no-underline">{{ piece.text }}</ins>
                  }
                  @case ('arrow') {
                    <span class="inline-block rtl:-scale-x-100">{{ piece.text }}</span>
                  }
                  @default {
                    <span>{{ piece.text }}</span>
                  }
                }
              }
            </dd>
          </div>
        }
      </dl>
    }
  </section>`,
  host: { class: 'block' },
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class StateChanges {
  readonly changes = input.required<EntityChanges>();
  readonly locale = input.required<Locale>();
  /** The page's own names of the fields' subjects, when the API gives none. */
  readonly names = input<SubjectNames>();

  private readonly translate = injectTranslate();
  protected readonly title = computed(() => {
    const kind = this.changes().kind;
    return kind === 'baseline' ? '' : this.translate()(TITLE_KEYS[kind]);
  });
  protected readonly compared = computed(() => {
    const version = this.changes().comparedVersion;
    return version === null ? null : this.translate()('detail.changes.compared', { version });
  });
  protected readonly rows = computed(() => {
    const format = { locale: this.locale(), translate: this.translate() };
    return changeRowsOf(this.changes().fields, format, this.names());
  });
}
