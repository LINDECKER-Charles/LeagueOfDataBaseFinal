import type { ItemCard } from '../../../../core/api/generated/models/item-card';
import type { ItemDetails } from '../../../../core/api/generated/models/item-details';
import { itemJsonLd } from '../../../../core/seo/json-ld/game/item-json-ld';
import { previewImage } from '../../../../core/seo/images/preview-image';
import type { SeoPage } from '../../../../core/seo/seo-page';
import type { CatalogueEntry } from '../../../../core/routing/catalogue/catalogue-entry';
import type { CatalogueTexts } from '../../shared/codex/head/catalogue-texts';
import { detailTrail } from '../../shared/codex/head/detail-trail';
import { mapLabelKey } from './aside/map-label-key';
import { itemDescriptionOf } from './item-description-of';

/** Summoner's Rift and Howling Abyss: an item on either is the one its bare name is sought by. */
const RIFT_MAPS: readonly number[] = [11, 12];

/**
 * The head of an item page. A LoL Classic twin shares its name with the current item, and so
 * does a variant kept to another mode (the Arena's Titanic Hydra): its edition or its modes are
 * part of the name its title, description and structured data give it, so the two pages never
 * read as duplicates.
 */
export function itemHeadOf(entry: CatalogueEntry<ItemDetails>, texts: CatalogueTexts): SeoPage {
  const { context, details } = entry;
  const card = details.profile;
  const qualifier = qualifierOf(card, texts, context.locale);
  const name = qualifier ? `${card.name} (${qualifier})` : card.name;
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

// What sets the item apart from its namesake: its edition, or the modes it is kept to. An item
// without maps names none, nor does a Rift variant (323075): Data Dragon flags it on the Rift
// like its namesake, with no other field to tell them apart.
function qualifierOf(card: ItemCard, texts: CatalogueTexts, locale: string): string | null {
  if (card.edition === 'classic') {
    return texts.main('edition.classic');
  }
  if (card.maps.length === 0 || card.maps.some((map) => RIFT_MAPS.includes(map))) {
    return null;
  }
  const modes = card.maps.map((map) => texts.main(mapLabelKey(map)));
  return new Intl.ListFormat(locale, { type: 'unit', style: 'narrow' }).format(modes);
}
