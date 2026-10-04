import type { ChampionDetails } from '../../../../core/api/generated/models/champion-details';
import type { CatalogueEntry } from '../../../../core/routing/catalogue/catalogue-entry';
import { championJsonLd } from '../../../../core/seo/json-ld/game/champion-json-ld';
import { breadcrumbList } from '../../../../core/seo/json-ld/site/breadcrumb-list';
import type { SeoPage } from '../../../../core/seo/seo-page';
import { CHAMPIONS_PREVIEW } from './champions-preview';
import type { Translate } from './translate';

/**
 * The head of a champion page: its texts from the `seo` scope, self-canonical on a pinned
 * version, a breadcrumb home › champions › champion, and the champion as a game character.
 */
export function championSeo(entry: CatalogueEntry<ChampionDetails>, translate: Translate): SeoPage {
  const { context, details } = entry;
  const name = details.profile.name;
  return {
    title: translate('seo.champion.detail.title', { name }),
    description: translate('seo.champion.detail.description', { name, version: context.version }),
    locale: context.locale,
    image: CHAMPIONS_PREVIEW,
    path: details.canonicalPath,
    version: context.pinned ? context.version : null,
    jsonLd: (urls) => [
      breadcrumbList([
        { name: translate('header.navigation.home'), url: `${urls.origin}/${context.locale}/` },
        { name: translate('header.navigation.champion'), url: urls.page('champions') },
        { name, url: urls.canonical },
      ]),
      championJsonLd(details, urls),
    ],
  };
}
