import type { ResourceType } from '../../../core/api/generated/models/resource-type';
import type { HomeLink } from '../data/home-link';
import type { HomeSection } from '../data/home-section';
import type { Preview } from '../data/preview';
import type { Previews } from '../data/previews';

function sectionOf(
  resource: ResourceType,
  preview: Preview | null,
  link: (path: string) => HomeLink,
): HomeSection {
  return {
    resource,
    list: link(resource),
    total: preview?.total ?? null,
    cards: (preview?.entries ?? []).map((entry) => ({
      link: link(entry.path),
      name: entry.name,
      caption: entry.caption,
      image: entry.image.url ?? null,
    })),
  };
}

/** The four sections of the home, their links made by `link` (see `linkMakerOf`). */
export function sectionsOf(
  previews: Previews,
  link: (path: string) => HomeLink,
): Readonly<Record<ResourceType, HomeSection>> {
  return {
    champions: sectionOf('champions', previews.champions, link),
    items: sectionOf('items', previews.items, link),
    runes: sectionOf('runes', previews.runes, link),
    summoners: sectionOf('summoners', previews.summoners, link),
  };
}
