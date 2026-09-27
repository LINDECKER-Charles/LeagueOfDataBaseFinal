import type { ResourceType } from '../../../core/api/generated/models/resource-type';

/** What the home says of a resource. */
interface ResourceTexts {
  /** Heading of the portal and of the preview. */
  readonly title: string;
  /** Label of the link to the list. */
  readonly seeAll: string;
  /**
   * What the framed empty state of an empty preview says; null where the legacy home left
   * the preview empty under its heading (items, spells).
   */
  readonly noData: string | null;
}

/** The keys of each resource, spelled out whole so the catalogue report finds them. */
export const RESOURCE_TEXTS: Readonly<Record<ResourceType, ResourceTexts>> = {
  champions: {
    title: 'homepage.champions.title',
    seeAll: 'homepage.champions.see_all',
    noData: 'homepage.champions.no_data',
  },
  items: {
    title: 'homepage.items.title',
    seeAll: 'homepage.items.see_all',
    noData: null,
  },
  runes: {
    title: 'homepage.runes.title',
    seeAll: 'homepage.runes.see_all',
    noData: 'home.runes.no_data',
  },
  summoners: {
    title: 'homepage.summoners.title',
    seeAll: 'homepage.summoners.see_all',
    noData: null,
  },
};
