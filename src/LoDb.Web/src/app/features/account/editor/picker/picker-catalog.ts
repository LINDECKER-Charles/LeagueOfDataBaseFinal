import { Injectable, inject } from '@angular/core';
import { type Observable, firstValueFrom, map } from 'rxjs';
import type { FavoriteSlot } from '../../../../core/api/generated/models/favorite-slot';
import { PickersService } from '../../../../core/api/generated/services/pickers.service';
import { flatEntriesOf } from '../entries/flat-entries-of';
import type { PickerEntry } from '../entries/picker-entry';
import { runeEntriesOf } from '../entries/rune-entries-of';
import { skinEntriesOf } from '../entries/skin-entries-of';
import type { SkinEntry } from '../entries/skin-entry';

/** The version and the language of the lists, those the favorites resolved on. */
interface CatalogScope {
  readonly version?: string;
  readonly lang?: string;
}

type SlotLoader = (
  pickers: PickersService,
  scope: CatalogScope,
) => Observable<readonly PickerEntry[]>;

const SLOT_LOADERS: Readonly<Record<FavoriteSlot, SlotLoader>> = {
  champion: (pickers, scope) =>
    pickers.pickChampions(scope).pipe(map(({ options }) => flatEntriesOf(options))),
  item: (pickers, scope) =>
    pickers.pickItems(scope).pipe(map(({ options }) => flatEntriesOf(options))),
  rune: (pickers, scope) => pickers.pickRunes(scope).pipe(map(({ trees }) => runeEntriesOf(trees))),
  summoner: (pickers, scope) =>
    pickers.pickSummoners(scope).pipe(map(({ options }) => flatEntriesOf(options))),
};

/**
 * The lists the favorite and skin pickers offer, each read on its first opening and kept for
 * the next ones. A list that failed is forgotten, so that trying again asks again.
 */
@Injectable()
export class PickerCatalog {
  private readonly pickers = inject(PickersService);
  private readonly lists = new Map<string, Promise<unknown>>();
  private scope: CatalogScope = {};

  /** Reads the lists of this version and language from now on. */
  use(version: string | null, lang: string | null): void {
    const scope = { version: version ?? undefined, lang: lang ?? undefined };
    if (scope.version !== this.scope.version || scope.lang !== this.scope.lang) {
      this.scope = scope;
      this.lists.clear();
    }
  }

  /** The options of a favorite slot; a rune path is one, followed by its runes. */
  favorites(slot: FavoriteSlot): Promise<readonly PickerEntry[]> {
    return this.cached(slot, () => SLOT_LOADERS[slot](this.pickers, this.scope));
  }

  /** The skins of a champion, its base skin first. */
  skins(championId: string): Promise<readonly SkinEntry[]> {
    return this.cached(`skins:${championId}`, () =>
      this.pickers
        .pickSkins({ ...this.scope, champion: championId })
        .pipe(map((picker) => skinEntriesOf(picker.skins))),
    );
  }

  private cached<T>(key: string, load: () => Observable<T>): Promise<T> {
    const known = this.lists.get(key) as Promise<T> | undefined;
    if (known !== undefined) {
      return known;
    }
    const loading = firstValueFrom(load());
    this.lists.set(key, loading);
    loading.catch(() => this.lists.delete(key));
    return loading;
  }
}
