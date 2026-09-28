import type { RuneTreeOption } from '../../../../core/api/generated/models/rune-tree-option';
import { imageSource } from './image-source';
import { normalizeSearchText } from './normalize-search-text';
import type { PickerEntry } from './picker-entry';

/** The rows of the rune picker: each path's header, then its runes slot by slot. */
export function runeEntriesOf(trees: readonly RuneTreeOption[]): PickerEntry[] {
  return trees.flatMap((tree) => {
    const groupId = String(tree.id);
    const header: PickerEntry = {
      id: groupId,
      name: tree.name,
      image: imageSource(tree.image),
      searchText: normalizeSearchText(tree.name),
      isGroup: true,
    };
    const runes = tree.slots.flat().map((rune) => ({
      id: String(rune.id),
      name: rune.name,
      image: imageSource(rune.image),
      searchText: normalizeSearchText(rune.name),
      groupId,
    }));
    return [header, ...runes];
  });
}
