/**
 * The context a visitor asked the switcher to remember (L3.9). The values are only
 * candidates: whoever applies them checks them against `/api/meta` first.
 */
export interface Preferences {
  /** Data Dragon language variant, such as `en_GB`, or null for the locale's own. */
  readonly lang: string | null;
  /** Version to read, or null to follow the latest. */
  readonly version: string | null;
}
