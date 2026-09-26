import type { SummonerDetails } from '../../../../core/api/generated/models/summoner-details';
import { summonerSpellJsonLd } from '../../../../core/seo/json-ld/game/summoner-spell-json-ld';
import type { CatalogueEntry } from '../../../../core/routing/catalogue/catalogue-entry';
import type { SeoPage } from '../../../../core/seo/seo-page';
import type { CatalogueTexts } from '../../items/codex/head/catalogue-texts';
import { detailTrail } from '../../items/codex/head/detail-trail';

/**
 * The head of a summoner spell page. A LoL Classic twin shares its name with the current
 * spell, so its edition is part of the name its title, description and structured data give
 * it: the two pages never read as duplicates.
 */
export function summonerHeadOf(
  entry: CatalogueEntry<SummonerDetails>,
  texts: CatalogueTexts,
): SeoPage {
  const { context, details } = entry;
  const card = details.profile;
  const edition = card.edition === 'classic' ? ` (${texts.main('edition.classic')})` : '';
  const name = `${card.name}${edition}`;
  const trail = {
    locale: context.locale,
    homeName: texts.main('header.navigation.home'),
    listName: texts.main('header.navigation.summoner'),
    listPath: 'summoners',
    name,
  };
  return {
    title: texts.seo('summoner.detail.title', { name }),
    description: texts.seo('summoner.detail.description', { name, version: context.version }),
    locale: context.locale,
    path: details.canonicalPath,
    version: context.pinned ? context.version : null,
    image: '/preview/summoners.png',
    jsonLd: (urls) => [
      detailTrail(trail, urls),
      summonerSpellJsonLd({ ...details, profile: { ...card, name } }, urls),
    ],
  };
}
