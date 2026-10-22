import type { RuneTreeDetails } from '../../../../core/api/generated/models/rune-tree-details';
import { runePathJsonLd } from '../../../../core/seo/json-ld/game/rune-path-json-ld';
import type { CatalogueEntry } from '../../../../core/routing/catalogue/catalogue-entry';
import { previewImage } from '../../../../core/seo/images/preview-image';
import type { SeoPage } from '../../../../core/seo/seo-page';
import type { CatalogueTexts } from '../../shared/codex/head/catalogue-texts';
import { detailTrail } from '../../shared/codex/head/detail-trail';
import { stateHeadOf } from '../../shared/codex/state/head/state-head-of';

/** The head of a rune path page; its preview is the path's own mark, as on the legacy site. */
export function runeHeadOf(entry: CatalogueEntry<RuneTreeDetails>, texts: CatalogueTexts): SeoPage {
  const { context, details } = entry;
  const { name, image } = details.profile;
  const trail = {
    locale: context.locale,
    homeName: texts.main('header.navigation.home'),
    listName: texts.main('header.navigation.runes'),
    listPath: 'runes',
    name,
  };
  return {
    title: texts.seo('rune.detail.title', { name }),
    ...stateHeadOf(
      entry,
      texts,
      texts.seo('rune.detail.description', { name, version: context.version }),
    ),
    locale: context.locale,
    path: details.canonicalPath,
    version: context.pinned ? context.version : null,
    canonical: details.canonical,
    image: image.status === 'present' && image.url ? image.url : previewImage('runes'),
    jsonLd: (urls) => [detailTrail(trail, urls), runePathJsonLd(details, urls)],
  };
}
