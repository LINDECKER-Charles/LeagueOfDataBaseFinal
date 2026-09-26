import type { ResourceType } from '../../../core/api/generated/models/resource-type';
import type { IconName } from '../../../ui/media/icon-name';

/** What the home says and shows of a resource. */
interface ResourceTexts {
  /** Heading of the portal and of the preview. */
  readonly title: string;
  /** Label of the link to the list. */
  readonly seeAll: string;
  readonly icon: IconName;
}

/** The keys of each resource, spelled out whole so the catalogue report finds them. */
export const RESOURCE_TEXTS: Readonly<Record<ResourceType, ResourceTexts>> = {
  champions: {
    title: 'homepage.champions.title',
    seeAll: 'homepage.champions.see_all',
    icon: 'champion',
  },
  items: { title: 'homepage.items.title', seeAll: 'homepage.items.see_all', icon: 'item' },
  runes: { title: 'homepage.runes.title', seeAll: 'homepage.runes.see_all', icon: 'rune' },
  summoners: {
    title: 'homepage.summoners.title',
    seeAll: 'homepage.summoners.see_all',
    icon: 'spell',
  },
};
