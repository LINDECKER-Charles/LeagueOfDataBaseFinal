import { ChangeDetectionStrategy, Component, computed, inject, input } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { RouterLink } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { catchError, of } from 'rxjs';
import type { CanonicalLink } from '../../../../../core/api/generated/models/canonical-link';
import { ApiMeta } from '../../../../../core/api/meta/api-meta';
import type { PageContext } from '../../../../../core/context/page-context';
import { injectCatalogueLink } from '../links/inject-catalogue-link';
import { previousVersionsOf } from './previous-versions-of';

/** How many listed versions before the page's own the block links. */
const PREVIOUS_PATCH_COUNT = 6;

/**
 * "In previous patches" under a detail page: the same entry in the six versions listed
 * before the page's, so that crawlers reach the past patches by links, the version selector
 * being a form. Links only, from the `/api/meta` the page's resolver already read: no
 * catalogue of those versions is loaded to render them. The canonical path is kept as is,
 * the id deciding: a renamed entry answers its slug's 301 there, a missing one its list's 302.
 * A version the latest one links to its short URL; the regional variant is carried on.
 * A pinned page also links the entry at the latest patch, the page its canonical names, so
 * that crawlers coming in by an old URL reach the page the sitemap lists: only when the API
 * places the entity there, a retired one having no such page.
 */
@Component({
  selector: 'lodb-previous-patches',
  imports: [RouterLink, TranslocoPipe],
  template: `@if (latest(); as latest) {
      <p class="mb-5 flex flex-wrap items-center gap-2">
        {{ 'detail.history.latest' | transloco }}
        <a class="hx-chip nav-link px-3 py-1.5 hover:text-hex" [routerLink]="latest.url">{{
          latest.version
        }}</a>
      </p>
    }
    @if (links().length > 0) {
      <nav aria-labelledby="previous-patches">
        <header class="codex-header mb-5">
          <h2 id="previous-patches">{{ 'detail.history.title' | transloco }}</h2>
        </header>
        <div class="flex flex-wrap gap-2">
          @for (link of links(); track link.version) {
            <a class="hx-chip nav-link px-3 py-1.5 hover:text-hex" [routerLink]="link.url">{{
              link.version
            }}</a>
          }
        </div>
      </nav>
    }`,
  host: { class: 'block' },
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PreviousPatches {
  readonly context = input.required<PageContext>();
  /** The entry's `canonicalPath` in the page's version. */
  readonly path = input.required<string>();
  /** The detail's `canonical`: where search engines index its entity. */
  readonly canonical = input<CanonicalLink | null>();

  private readonly linkTo = injectCatalogueLink();
  // Already answered for the page's own resolver: read again, it replays at once.
  private readonly meta = toSignal(
    inject(ApiMeta)
      .meta()
      .pipe(catchError(() => of(null))),
    { initialValue: null },
  );

  protected readonly links = computed(() => {
    const meta = this.meta();
    const context = this.context();
    const versions = meta === null ? [] : meta.versions;
    return previousVersionsOf(versions, context.version, PREVIOUS_PATCH_COUNT).map((version) => {
      const pinned = version !== meta?.latest;
      return { version, url: this.linkTo({ ...context, version, pinned }, this.path()) };
    });
  });

  protected readonly latest = computed(() => {
    const version = this.meta()?.latest;
    const context = this.context();
    const canonical = this.canonical();
    if (!version || !context.pinned || canonical?.version !== null) {
      return null;
    }
    return { version, url: this.linkTo({ ...context, version, pinned: false }, canonical.path) };
  });
}
