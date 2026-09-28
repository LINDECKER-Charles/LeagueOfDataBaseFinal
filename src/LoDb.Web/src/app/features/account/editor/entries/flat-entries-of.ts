import type { CatalogImage } from '../../../../core/api/generated/models/catalog-image';
import { imageSource } from './image-source';
import { normalizeSearchText } from './normalize-search-text';
import type { PickerEntry } from './picker-entry';

/** An option of the champion, item or summoner spell picker, as the API lists it. */
interface FlatOption {
  readonly id: string;
  readonly name: string;
  readonly image: CatalogImage;
}

/** The rows of a flat picker. The id joins the search, so `flash` finds `SummonerFlash`. */
export function flatEntriesOf(options: readonly FlatOption[]): PickerEntry[] {
  return options.map((option) => ({
    id: option.id,
    name: option.name,
    image: imageSource(option.image),
    searchText: normalizeSearchText(`${option.name} ${option.id}`),
  }));
}
