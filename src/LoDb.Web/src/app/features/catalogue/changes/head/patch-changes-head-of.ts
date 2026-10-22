import type { CanonicalLink } from '../../../../core/api/generated/models/canonical-link';
import type { PatchChanges } from '../../../../core/api/generated/models/patch-changes';
import type { CatalogueEntry } from '../../../../core/routing/catalogue/catalogue-entry';
import { PATCH_CHANGES_PATH } from '../../../../core/routing/patch-changes-path';
import { breadcrumbList } from '../../../../core/seo/json-ld/site/breadcrumb-list';
import { itemList } from '../../../../core/seo/json-ld/site/item-list';
import type { SeoPage } from '../../../../core/seo/seo-page';
import type { SeoUrls } from '../../../../core/seo/urls/seo-urls';
import type { Translate } from '../../shared/codex/texts/translate';

/** The page of the patch history, below the locale. */
const PATCH_HISTORY_PATH = 'patches';

// An entry's page, absolute: the version it names, or the short URL of the latest one.
function pageUrl(urls: SeoUrls, locale: string, link: CanonicalLink): string {
  const version = link.version === null ? '' : `${link.version}/`;
  return `${urls.origin}/${locale}/${version}${link.path}`;
}

// The trail home › patch history › patch, then the entries the patch changed.
function jsonLdOf(entry: CatalogueEntry<PatchChanges>, title: string, translate: Translate) {
  const { locale } = entry.context;
  const entries = entry.details.groups.flatMap((group) => group.entries);
  return (urls: SeoUrls) => [
    breadcrumbList([
      { name: translate('header.navigation.home'), url: `${urls.origin}/${locale}/` },
      { name: translate('patches.title'), url: `${urls.origin}/${locale}/${PATCH_HISTORY_PATH}` },
      { name: title, url: urls.canonical },
    ]),
    itemList(
      entries.map((change) => ({ name: change.name, url: pageUrl(urls, locale, change.link) })),
    ),
  ];
}

/**
 * The head of the page of a patch's changes: its own canonical (a past patch's given
 * explicitly, the default being the same page at the latest patch), indexable when the patch
 * changed something, `noindex, follow` otherwise; its title names its patch, so no other is
 * added. Its structured data: the trail home › patch history › patch, and its entries.
 */
export function patchChangesHeadOf(
  entry: CatalogueEntry<PatchChanges>,
  translate: Translate,
): SeoPage {
  const { context, details } = entry;
  const { locale } = context;
  const params = { version: details.version, previous: details.previousVersion };
  const title = translate('patches.changes.title', params);
  const pinned = context.pinned ? context.version : null;
  return {
    kind: details.hasChanges ? 'page' : 'noindex',
    title,
    description: translate(
      details.hasChanges ? 'patches.changes.description' : 'patches.changes.description_empty',
      params,
    ),
    locale,
    path: PATCH_CHANGES_PATH,
    version: pinned,
    ...(pinned === null ? {} : { canonical: { version: pinned, path: PATCH_CHANGES_PATH } }),
    titlePatches: false,
    jsonLd: jsonLdOf(entry, title, translate),
  };
}
