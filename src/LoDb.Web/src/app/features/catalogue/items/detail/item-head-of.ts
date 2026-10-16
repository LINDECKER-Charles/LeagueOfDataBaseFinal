import type { ItemDetails } from '../../../../core/api/generated/models/item-details';
import { itemJsonLd } from '../../../../core/seo/json-ld/game/item-json-ld';
import { previewImage } from '../../../../core/seo/images/preview-image';
import type { SeoPage } from '../../../../core/seo/seo-page';
import type { CatalogueEntry } from '../../../../core/routing/catalogue/catalogue-entry';
import type { CatalogueTexts } from '../../shared/codex/head/catalogue-texts';
import { detailTrail } from '../../shared/codex/head/detail-trail';
import { itemDescriptionOf } from './item-description-of';

/**
 * The head of an item page. A LoL Classic twin shares its name with the current item, so its
 * edition is part of the name its title, description and structured data give it: the two
 * pages never read as duplicates.
 */
export function itemHeadOf(entry: CatalogueEntry<ItemDetails>, texts: CatalogueTexts): SeoPage {
  const { context, details } = entry;
  const card = details.profile;
  const edition = card.edition === 'classic' ? ` (${texts.main('edition.classic')})` : '';
  const name = `${card.name}${edition}`;
  const trail = {
    locale: context.locale,
    homeName: texts.main('header.navigation.home'),
    listName: texts.main('header.navigation.item'),
    listPath: 'items',
    name,
  };
  return {
    title: texts.seo('item.detail.title', { name }),
    description: itemDescriptionOf(entry, texts, name),
    locale: context.locale,
    path: details.canonicalPath,
    version: context.pinned ? context.version : null,
    canonical: details.canonical ?? null,
    image: previewImage('items'),
    jsonLd: (urls) => [
      detailTrail(trail, urls),
      itemJsonLd({ ...details, profile: { ...card, name } }, urls),
    ],
  };
}
