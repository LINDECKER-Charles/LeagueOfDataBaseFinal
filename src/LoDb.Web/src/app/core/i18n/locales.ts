import type { UiLocale } from '../api/generated/models/ui-locale';
import { UI_LOCALE } from '../api/generated/models/ui-locale-array';

/** A site locale, in its URL form (ADR 0005). */
export type Locale = UiLocale;

/**
 * The 21 site locales, generated from the API contract (`UiLocale` in LoDb.Domain), so the
 * list is never maintained twice (plan, section 5.3).
 */
export const LOCALES: readonly Locale[] = UI_LOCALE;
