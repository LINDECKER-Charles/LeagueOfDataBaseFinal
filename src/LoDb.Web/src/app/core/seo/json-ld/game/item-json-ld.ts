import type { ItemDetails } from '../../../api/generated/models/item-details';
import type { SeoUrls } from '../../urls/seo-urls';
import type { JsonLdNode } from '../core/json-ld-node';
import { plainText } from '../core/plain-text';
import { propertyValues } from '../core/property-values';
import { pruneJsonLd } from '../core/prune-json-ld';
import { imageOf } from './image-of';
import { videoGame } from './video-game';

const GOLD = 'gold';

/** An item page: the item as a `gameItem`, with its gold economy and categories. */
export function itemJsonLd(item: ItemDetails, urls: SeoUrls): JsonLdNode {
  const card = item.profile;
  const gold = card.gold;
  const entity = pruneJsonLd({
    '@type': 'Thing',
    '@id': `${urls.canonical}#item`,
    name: card.name,
    image: imageOf(card.image, urls),
    description: plainText(card.summary) ?? plainText(item.description),
    additionalProperty: [
      ...propertyValues('Total cost', [gold.total], GOLD),
      ...propertyValues('Combine cost', [gold.base], GOLD),
      ...propertyValues('Sell value', [gold.sell], GOLD),
      ...propertyValues('Category', card.tags),
      // Only the exception is worth stating: most items can be bought.
      ...propertyValues('Purchasable', gold.isPurchasable ? [] : [false]),
    ],
  });
  return videoGame('gameItem', entity, urls.canonical);
}
