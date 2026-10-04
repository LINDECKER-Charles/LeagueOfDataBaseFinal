import type { IconName } from '../../../ui/media/icon-name';

/** A destination of the chrome: its path under the locale, its label key, its glyph. */
export interface NavEntry {
  /** Path after `/{locale}`, empty for the home page. */
  readonly path: string;
  /** Translation key of the label. */
  readonly label: string;
  readonly icon?: IconName;
}
