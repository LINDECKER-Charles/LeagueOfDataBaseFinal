import type { Tone } from '../badge';

/**
 * A view of a page as its tab shows it (`StatusTabs`): its name, its key figure and what
 * qualifies it, and the state worth a glance.
 */
export interface StatusTile {
  /** Names the tab and its panel, `{key}-tab` and `{key}-panel`: unique in the page. */
  readonly key: string;
  /** The view's name, translated. */
  readonly label: string;
  /** Its key figure, written: « 97,4 % », « Aucune ». */
  readonly value: string;
  /** What the figure counts or what follows it, translated; null for nothing. */
  readonly sub: string | null;
  /**
   * A dot beside the name when the view has something to say: `live` while it runs, a tone
   * otherwise; null when all is quiet. The text says it too: the dot is never alone.
   */
  readonly tone: Tone | 'live' | null;
}
