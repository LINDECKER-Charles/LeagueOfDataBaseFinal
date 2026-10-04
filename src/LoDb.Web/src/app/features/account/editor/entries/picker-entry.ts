/** A row of a picker: a champion, an item, a spell, a rune or a whole rune path. */
export interface PickerEntry {
  readonly id: string;
  readonly name: string;
  /** Browser-ready URL of its image; null when the catalogue holds none. */
  readonly image: string | null;
  /** What a query is compared to, already normalized (`normalizeSearchText`). */
  readonly searchText: string;
  /** The rune path a rune belongs to: it indents the rune and ties it to its header. */
  readonly groupId?: string;
  /** A rune path's header, selectable too: a favorite may be a whole path. */
  readonly isGroup?: boolean;
}
