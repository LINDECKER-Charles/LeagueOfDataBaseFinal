import { Injectable, inject } from '@angular/core';
import { type Observable, firstValueFrom, map } from 'rxjs';
import type { ChampionOption } from '../../../../core/api/generated/models/champion-option';
import type { GameMode } from '../../../../core/api/generated/models/game-mode';
import type { ItemOption } from '../../../../core/api/generated/models/item-option';
import type { RuneTreeOption } from '../../../../core/api/generated/models/rune-tree-option';
import { PickersService } from '../../../../core/api/generated/services/pickers.service';
import type { PickerQuery } from './picker-query';

/**
 * The picker lists of the editor, one request per list, patch, language and, for the items,
 * game mode, kept for the whole visit: switching back to a patch or a mode is instant, and
 * a second editor opens on lists already read. A list that failed is forgotten, so that
 * trying again asks again.
 */
@Injectable({ providedIn: 'root' })
export class PickerCache {
  private readonly pickers = inject(PickersService);
  private readonly lists = new Map<string, Promise<unknown>>();

  champions(query: PickerQuery): Promise<readonly ChampionOption[]> {
    return this.cached(['champions', query.version, query.lang], () =>
      this.pickers.pickChampions(query).pipe(map((picker) => picker.options)),
    );
  }

  items(query: PickerQuery, mode: GameMode): Promise<readonly ItemOption[]> {
    return this.cached(['items', query.version, query.lang, mode], () =>
      this.pickers.pickItems({ ...query, mode }).pipe(map((picker) => picker.options)),
    );
  }

  runes(query: PickerQuery): Promise<readonly RuneTreeOption[]> {
    return this.cached(['runes', query.version, query.lang], () =>
      this.pickers.pickRunes(query).pipe(map((picker) => picker.trees)),
    );
  }

  private cached<T>(key: readonly string[], load: () => Observable<T>): Promise<T> {
    const id = key.join('|');
    const known = this.lists.get(id) as Promise<T> | undefined;
    if (known !== undefined) {
      return known;
    }
    const loading = firstValueFrom(load());
    this.lists.set(id, loading);
    loading.catch(() => this.lists.delete(id));
    return loading;
  }
}
