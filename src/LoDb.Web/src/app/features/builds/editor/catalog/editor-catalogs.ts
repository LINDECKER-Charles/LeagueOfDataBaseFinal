import { Injectable, computed, inject, linkedSignal, resource } from '@angular/core';
import type { ItemOption } from '../../../../core/api/generated/models/item-option';
import { BuildEditorStore } from '../form/build-editor-store';
import { EDITOR_ENTRY } from '../form/editor-entry-token';
import { ghostOf } from './ghost-of';
import type { GhostReason } from './ghost-reason';
import { PickerCache } from './picker-cache';
import { PickerList } from './picker-list';

type ItemsById = ReadonlyMap<string, ItemOption>;

function byId(items: readonly ItemOption[]): ItemsById {
  return new Map(items.map((item) => [item.id, item]));
}

/**
 * The picker lists of an editor, read on its current context: another patch reads all
 * three again, another mode the items only, another language none. Every item a list held
 * stays known for the visit, so a placed item keeps its name, icon and gold after a switch,
 * and a ghost tells an item this mode excludes from one the patch lacks.
 */
@Injectable()
export class EditorCatalogs {
  private readonly cache = inject(PickerCache);
  private readonly store = inject(BuildEditorStore);
  private readonly lang = inject(EDITOR_ENTRY).lang;
  private readonly query = computed(() => ({ version: this.store.gameVersion(), lang: this.lang }));

  readonly champions = new PickerList(
    resource({ params: this.query, loader: ({ params }) => this.cache.champions(params) }),
  );
  readonly items = new PickerList(
    resource({
      params: () => ({ query: this.query(), mode: this.store.gameMode() }),
      loader: ({ params }) => this.cache.items(params.query, params.mode),
    }),
  );
  readonly runes = new PickerList(
    resource({ params: this.query, loader: ({ params }) => this.cache.runes(params) }),
  );

  private readonly current = computed(() => {
    const options = this.items.options();
    return options === null ? null : byId(options);
  });
  private readonly known = linkedSignal<ItemsById | null, ItemsById>({
    source: this.current,
    computation: (current, previous) => new Map([...(previous?.value ?? []), ...(current ?? [])]),
  });

  /** A placed item as a list knows it: the current one first, then any read this visit. */
  resolveItem(itemId: string): ItemOption | undefined {
    return this.current()?.get(itemId) ?? this.known().get(itemId);
  }

  ghostOf(itemId: string): GhostReason {
    return ghostOf(itemId, this.current(), this.known());
  }
}
