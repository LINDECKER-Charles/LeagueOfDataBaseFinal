import type { RuneTreeDetails } from '../../../api/generated/models/rune-tree-details';
import type { SeoUrls } from '../../urls/seo-urls';
import type { JsonLdNode } from '../core/json-ld-node';
import { propertyValues } from '../core/property-values';
import { pruneJsonLd } from '../core/prune-json-ld';
import { imageOf } from './image-of';
import { videoGame } from './video-game';

/** A rune path page: the path as a `gameItem`, with the number of its slots. */
export function runePathJsonLd(tree: RuneTreeDetails, urls: SeoUrls): JsonLdNode {
  const slots = tree.slots.length;
  const entity = pruneJsonLd({
    '@type': 'Thing',
    '@id': `${urls.canonical}#rune-path`,
    name: tree.profile.name,
    image: imageOf(tree.profile.image, urls),
    additionalProperty: propertyValues('Slots', slots === 0 ? [] : [slots]),
  });
  return videoGame('gameItem', entity, urls.canonical);
}
