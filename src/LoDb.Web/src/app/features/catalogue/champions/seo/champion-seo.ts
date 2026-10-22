import type { ChampionDetails } from '../../../../core/api/generated/models/champion-details';
import type { CatalogueEntry } from '../../../../core/routing/catalogue/catalogue-entry';
import { championJsonLd } from '../../../../core/seo/json-ld/game/champion-json-ld';
import { splashImage } from '../../../../core/seo/images/splash-image';
import { breadcrumbList } from '../../../../core/seo/json-ld/site/breadcrumb-list';
import type { SeoPage } from '../../../../core/seo/seo-page';
import type { CatalogueTexts } from '../../shared/codex/head/catalogue-texts';
import { stateHeadOf } from '../../shared/codex/state/head/state-head-of';
import type { Translate } from './translate';

/** The scope the champions' head texts are read under (`public/i18n/seo/`). */
const SEO_PREFIX = 'seo.';

/**
 * The head of a champion page: its texts from the `seo` scope, its splash art as the preview
 * of its links, on a pinned version the canonical the API places the champion at and, on the
 * opener of a closed state, its run of patches and what its patch changed, a breadcrumb home
 * › champions › champion, and the champion as a game character.
 */
export function championSeo(entry: CatalogueEntry<ChampionDetails>, translate: Translate): SeoPage {
  const { context, details } = entry;
  const name = details.profile.name;
  const texts: CatalogueTexts = {
    seo: (key, params) => translate(`${SEO_PREFIX}${key}`, params),
    main: translate,
  };
  const description = texts.seo('champion.detail.description', { name, version: context.version });
  return {
    title: texts.seo('champion.detail.title', { name }),
    ...stateHeadOf(entry, texts, description),
    locale: context.locale,
    image: splashImage(details.art.splash, translate('champion.detail.splash_alt', { name })),
    path: details.canonicalPath,
    version: context.pinned ? context.version : null,
    canonical: details.canonical,
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
