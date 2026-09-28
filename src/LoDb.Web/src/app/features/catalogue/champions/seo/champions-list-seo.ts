import type { ChampionList } from '../../../../core/api/generated/models/champion-list';
import type { PageContext } from '../../../../core/context/page-context';
import type { SeoPage } from '../../../../core/seo/seo-page';
import { catalogueListSeo } from '../../shared/seo/catalogue-list-seo';
import { CHAMPIONS_PREVIEW } from './champions-preview';
import type { Translate } from './translate';

/**
 * The head of the champions list: its size and version, and the champions it opens on. A
 * list the API did not answer keeps its title and falls back to the site's description.
 */
export function championsListSeo(
  context: PageContext,
  list: ChampionList | null,
  translate: Translate,
): SeoPage {
  const description = list
    ? translate('seo.champion.list.description', { count: list.total, version: context.version })
    : undefined;
  const page = catalogueListSeo({
    title: translate('seo.champion.list.title'),
    description: description ?? '',
    path: 'champions',
    context,
    entries: list?.entries ?? [],
  });
  return { ...page, description, image: CHAMPIONS_PREVIEW };
}
