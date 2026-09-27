import type { ChampionList } from '../../../core/api/generated/models/champion-list';
import type { ItemList } from '../../../core/api/generated/models/item-list';
import type { RuneList } from '../../../core/api/generated/models/rune-list';
import type { SummonerList } from '../../../core/api/generated/models/summoner-list';
import type { Preview } from '../data/preview';
import { PREVIEW_SIZE } from './preview-size';

// An item is told apart by its id, as the legacy cards did: Classic twins share a name.
const ITEM_CAPTION_PREFIX = 'id ';

/**
 * The preview of each resource, from the first page of its list. Runes preview and count
 * their paths, the pages a rune links to, as the legacy home did: its count was the size of
 * the list it previewed, not the runes the paths hold.
 */
export const PREVIEW_OF = {
  champions: (list: ChampionList): Preview => ({
    total: list.total,
    entries: list.entries.slice(0, PREVIEW_SIZE).map((champion) => ({
      path: champion.canonicalPath,
      name: champion.name,
      caption: champion.title,
      image: champion.image,
      edition: 'modern',
      art: champion.loadingArt,
    })),
  }),
  items: (list: ItemList): Preview => ({
    total: list.total,
    entries: list.entries.slice(0, PREVIEW_SIZE).map((item) => ({
      path: item.canonicalPath,
      name: item.name,
      caption: `${ITEM_CAPTION_PREFIX}${item.id}`,
      image: item.image,
      edition: item.edition,
      art: null,
    })),
  }),
  runes: (list: RuneList): Preview => ({
    total: list.trees.length,
    entries: list.trees.slice(0, PREVIEW_SIZE).map((tree) => ({
      path: tree.canonicalPath,
      name: tree.name,
      caption: tree.key,
      image: tree.image,
      edition: 'modern',
      art: null,
    })),
  }),
  summoners: (list: SummonerList): Preview => ({
    total: list.total,
    entries: list.entries.slice(0, PREVIEW_SIZE).map((spell) => ({
      path: spell.canonicalPath,
      name: spell.name,
      caption: spell.id,
      image: spell.image,
      edition: spell.edition,
      art: null,
    })),
  }),
} as const;
