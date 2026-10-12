import type { Translation } from '@jsverse/transloco';
import FR from '../../../../public/i18n/patches/fr.json';

/**
 * The patch history's catalogue (`public/i18n/patches/`) its page spec renders with. Here
 * rather than in the feature, which imports nothing outside `core/`, `ui/` and itself.
 * Whether its texts are translated in every locale is `npm run test:i18n`'s to check.
 */
export const PATCHES_CATALOGUES = { fr: FR } satisfies Record<string, Translation>;
