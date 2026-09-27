import { Injectable, inject } from '@angular/core';
import { type Observable, catchError, forkJoin, map, of, switchMap } from 'rxjs';
import type { CatalogMeta } from '../../../../core/api/generated/models/catalog-meta';
import type { RuneList } from '../../../../core/api/generated/models/rune-list';
import { CatalogService } from '../../../../core/api/generated/services/catalog.service';
import { ApiMeta } from '../../../../core/api/meta/api-meta';
import { languageOf } from '../../../../core/api/meta/language-of';
import type { Locale } from '../../../../core/i18n/locales';
import type { InventorySnapshot } from './inventory-snapshot';

const UNKNOWN: InventorySnapshot = {
  version: null,
  champions: null,
  items: null,
  runes: null,
  summoners: null,
  languages: null,
  versions: null,
};

// A one-entry page answers the size of the whole list without shipping it.
const COUNT_PAGE = { page: 1, size: 1 };

function count<T>(list: Observable<T>, size: (page: T) => number): Observable<number | null> {
  return list.pipe(
    map(size),
    catchError(() => of(null)),
  );
}

function totalOf(page: { readonly total: number }): number {
  return page.total;
}

// The runes count as their paths, as the legacy inventory did: every page lists them all.
function pathsOf(page: RuneList): number {
  return page.trees.length;
}

/**
 * The inventory of the About pages, asked of the API by the browser only: the pages are
 * prerendered, and a prerender never calls the API. Each count is read from the first page
 * of its list on the latest version, in the locale's language; the runes are their paths.
 */
@Injectable({ providedIn: 'root' })
export class Inventory {
  private readonly meta = inject(ApiMeta);
  private readonly catalog = inject(CatalogService);

  snapshot(locale: Locale): Observable<InventorySnapshot> {
    return this.meta.meta().pipe(
      switchMap((meta) => this.counts(meta, locale)),
      catchError(() => of(UNKNOWN)),
    );
  }

  private counts(meta: CatalogMeta, locale: Locale): Observable<InventorySnapshot> {
    const known = { languages: meta.languages.length, versions: meta.versions.length };
    const version = meta.latest ?? null;
    if (version === null) {
      return of({ ...UNKNOWN, ...known });
    }
    const list = { version, lang: languageOf(meta, locale), ...COUNT_PAGE };
    return forkJoin({
      champions: count(this.catalog.listChampions(list), totalOf),
      items: count(this.catalog.listItems(list), totalOf),
      runes: count(this.catalog.listRunes(list), pathsOf),
      summoners: count(this.catalog.listSummoners(list), totalOf),
    }).pipe(map((counts) => ({ version, ...counts, ...known })));
  }
}
