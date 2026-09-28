import type { CanMatchFn } from '@angular/router';
import { isLocale } from './is-locale';

/**
 * Lets `:locale` match only one of the 21 locales. A guard rather than a `matcher`, which
 * route extraction and prerendering cannot follow (plan, section 5.2).
 */
export const localeGuard: CanMatchFn = (_route, segments) => isLocale(segments[0]?.path);
