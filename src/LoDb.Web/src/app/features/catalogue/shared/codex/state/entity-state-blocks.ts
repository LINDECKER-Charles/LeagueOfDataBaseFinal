import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import type { PageContext } from '../../../../../core/context/page-context';
import { injectTranslate } from '../texts/inject-translate';
import { StateChanges } from './changes/state-changes';
import { StateHistory } from './history/state-history';
import { subjectNamesOf } from './labels/subject-names-of';
import { injectStateLink } from './links/inject-state-link';
import type { StatefulDetails } from './stateful-details';

/**
 * What a detail page says of its entity's states (docs/produit/seo-une-page-par-etat.md):
 * on a pinned page whose entity the latest patch holds, a link to it there, by its short URL
 * (never a version number, which would go stale in a stored page); then what the page's
 * patch changed when it opened the state, or since which patch nothing changed; then the
 * history. A frozen page (a closed state) thus links versioned URLs only, but « In the latest
 * patch »: its content depends on its state and those before, never on a later promotion.
 */
@Component({
  selector: 'lodb-entity-state',
  imports: [RouterLink, StateChanges, StateHistory],
  template: `@if (latestUrl(); as latestUrl) {
      <p class="mb-5">
        <a class="hx-chip nav-link gap-2 px-3 py-1.5 hover:text-hex" [routerLink]="latestUrl"
          >{{ latestLabel() }}
          <span class="inline-block rtl:-scale-x-100" aria-hidden="true">→</span></a
        >
      </p>
    }
    @if (shownChanges(); as changes) {
      <lodb-state-changes
        class="mb-14"
        [changes]="changes"
        [locale]="context().locale"
        [names]="names()"
      />
    } @else if (unchanged(); as unchanged) {
      <p class="mb-14">
        <a class="hx-chip nav-link px-3 py-1.5 hover:text-hex" [routerLink]="unchanged.url">{{
          unchanged.text
        }}</a>
      </p>
    }
    @if (details().history.length > 0) {
      <lodb-state-history [context]="context()" [details]="details()" [names]="names()" />
    }`,
  host: { class: 'block' },
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class EntityStateBlocks {
  readonly context = input.required<PageContext>();
  readonly details = input.required<StatefulDetails>();

  private readonly translate = injectTranslate();
  private readonly linkTo = injectStateLink();
  protected readonly names = computed(() => subjectNamesOf(this.details()));
  protected readonly latestLabel = computed(() => this.translate()('detail.history.latest'));
  // A state opened at the start of the index was compared to nothing: the API sends no
  // changes for it, and a page never shows them.
  protected readonly shownChanges = computed(() => {
    const changes = this.details().changes;
    return changes?.kind === 'baseline' ? null : changes;
  });

  // The entity at the latest patch: the open state's canonical path, or the page's own, the
  // id deciding (a renamed entity answers its slug's 301 there).
  protected readonly latestUrl = computed(() => {
    const { isPresentInLatest, canonical, canonicalPath } = this.details();
    const context = this.context();
    if (!context.pinned || !isPresentInLatest) {
      return null;
    }
    const path = canonical.version === null ? canonical.path : canonicalPath;
    return this.linkTo(context, { version: null, path });
  });

  // « Unchanged since patch X » on a page of a state it did not open, short URL included.
  protected readonly unchanged = computed(() => {
    const state = this.details().state;
    if (state === null || state.isOpener) {
      return null;
    }
    return {
      text: this.translate()('detail.changes.unchanged', { version: state.openerVersion }),
      url: this.linkTo(this.context(), state.openerLink),
    };
  });
}
