import type { SkinOption } from '../../../../core/api/generated/models/skin-option';
import { normalizeSearchText } from './normalize-search-text';
import type { SkinEntry } from './skin-entry';

/** The tiles of the skin step, searched by their name. */
export function skinEntriesOf(options: readonly SkinOption[]): SkinEntry[] {
  return options.map((option) => ({
    id: option.id,
    name: option.name,
    image: option.image,
    banner: option.banner,
    searchText: normalizeSearchText(option.name),
  }));
}
