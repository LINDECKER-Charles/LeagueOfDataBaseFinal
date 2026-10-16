import type { Translation } from '@jsverse/transloco';
import EN from '../../../../public/i18n/en.json';
import FR from '../../../../public/i18n/fr.json';
import JA from '../../../../public/i18n/ja.json';

/**
 * The root catalogues the specs render with, as authored (`public/i18n`), so a page shows its
 * real texts. Here rather than in a feature, which imports nothing outside `core/`, `ui/` and
 * itself. Whether a text is translated in every locale is `npm run test:i18n`'s to check.
 */
export const ROOT_CATALOGUES = { en: EN, fr: FR, ja: JA } satisfies Record<string, Translation>;
