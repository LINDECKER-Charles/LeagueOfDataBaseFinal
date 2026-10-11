import { type ResourceRef, inject } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { map, retry } from 'rxjs';
import type { ChampionSkin } from '../../../../../core/api/generated/models/champion-skin';
import { CatalogService } from '../../../../../core/api/generated/services/catalog.service';

/** The champion whose skins are read, in the version and language of its page. */
export interface SkinsScope {
  readonly version: string;
  readonly lang: string;
  readonly id: string;
}

// The delay the API asks for before trying a cold catalog again: one more try, never a loop.
const RETRY_DELAY_MS = 5_000;

/**
 * The skins of a champion, the base one first, read from the API by the component that
 * calls it: the gallery, created once the reader scrolls to it, never on the server. Read
 * again when the scope changes (the pager reuses the page for the next champion); one more
 * try after a failure, then the error.
 */
export function injectChampionSkins(
  scope: () => SkinsScope,
): ResourceRef<readonly ChampionSkin[] | undefined> {
  const catalog = inject(CatalogService);
  return rxResource({
    params: scope,
    stream: ({ params }) =>
      catalog.getChampionSkins(params).pipe(
        retry({ count: 1, delay: RETRY_DELAY_MS }),
        map((answer) => answer.skins),
      ),
  });
}
