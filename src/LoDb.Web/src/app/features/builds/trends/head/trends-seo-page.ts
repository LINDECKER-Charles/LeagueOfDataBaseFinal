import type { TrendRow } from '../../../../core/api/generated/models/trend-row';
import type { Locale } from '../../../../core/i18n/locales';
import type { JsonLdNode } from '../../../../core/seo/json-ld/core/json-ld-node';
import { breadcrumbList } from '../../../../core/seo/json-ld/site/breadcrumb-list';
import { itemList } from '../../../../core/seo/json-ld/site/item-list';
import type { SeoPage } from '../../../../core/seo/seo-page';
import type { SeoUrls } from '../../../../core/seo/urls/seo-urls';
import type { TrendsTexts } from './trends-texts';

function trendsJsonLd(rows: readonly TrendRow[], texts: TrendsTexts, urls: SeoUrls) {
  const nodes: JsonLdNode[] = [
    breadcrumbList([
      { name: texts.main('header.navigation.home'), url: urls.page('') },
      { name: texts.main('community.trends.title'), url: urls.canonical },
    ]),
  ];
  if (rows.length > 0) {
    const links = rows.map((row) => ({
      name: row.name,
      url: urls.absolute(`/b/${row.shareToken}`),
    }));
    nodes.push(itemList(links));
  }
  return nodes;
}

/**
 * The head of the trends: indexable, canonical without its query (filters and pages share the
 * address of the first), the trail from the home, and the builds of the page in vote order,
 * which live at `/b/{token}` outside the locales.
 */
export function trendsSeoPage(
  rows: readonly TrendRow[],
  texts: TrendsTexts,
  locale: Locale,
): SeoPage {
  return {
    title: texts.seo('trends.title'),
    description: texts.seo('trends.description'),
    locale,
    path: 'trends',
    jsonLd: (urls) => trendsJsonLd(rows, texts, urls),
  };
}
