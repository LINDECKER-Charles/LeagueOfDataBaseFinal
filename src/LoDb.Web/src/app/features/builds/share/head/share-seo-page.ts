import type { SharedBuild } from '../../../../core/api/generated/models/shared-build';
import type { Locale } from '../../../../core/i18n/locales';
import type { SeoPage } from '../../../../core/seo/seo-page';
import type { ShareTranslate } from './share-translate';

// The length a search snippet or a link preview shows of a description.
const DESCRIPTION_LENGTH = 160;

/**
 * The head of a shared build: "{build} · {champion}" as title, its description or a line
 * naming the champion. Unlisted, public or not (ADR 0005, `heritage.md` § 6, an assumed gap
 * with the legacy page, which indexed public builds): `noindex`, no canonical, no JSON-LD;
 * the Open Graph tags still make the link previews.
 */
export function shareSeoPage(
  build: SharedBuild,
  translate: ShareTranslate,
  locale: Locale,
): SeoPage {
  const champion = build.champion.name;
  const description = build.description?.trim()
    ? build.description.trim().slice(0, DESCRIPTION_LENGTH)
    : translate('build.show.meta_fallback', { champion });
  return { kind: 'share', title: `${build.name} · ${champion}`, description, locale };
}
