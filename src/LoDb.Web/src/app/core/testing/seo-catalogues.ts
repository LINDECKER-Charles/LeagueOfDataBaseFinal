import type { Translation } from '@jsverse/transloco';
import EN from '../../../../public/i18n/seo/en.json';
import JA from '../../../../public/i18n/seo/ja.json';

/**
 * The SEO catalogues (`public/i18n/seo/`) the head specs fill, as authored, so a description
 * is measured with its real texts. Here rather than in a feature, which imports nothing
 * outside `core/`, `ui/` and itself. Whether a text is translated in every locale is
 * `npm run test:i18n`'s to check.
 */
export const SEO_CATALOGUES = { en: EN, ja: JA } satisfies Record<string, Translation>;
