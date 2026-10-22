/** What a launch starts from: the page fills it, the dialog edits it. */
export interface LaunchPreset {
  /** Where the dialog was opened from, for its first step and its wording. */
  readonly origin: 'header' | 'idle' | 'coverage' | 'complete-epoch' | 'relaunch' | 'failures';
  /**
   * The versions ticked: 'all' for every older patch, 'incomplete' for those with a gap in
   * the epoch (a cell - or p, resolved once its coverage loads), [] for none.
   */
  readonly versions: readonly string[] | 'all' | 'incomplete';
  /** The versions to render first, in this order; a subset of `versions`. */
  readonly priority: readonly string[];
  /** The locales, in the site's order; null for all 21. */
  readonly locales: readonly string[] | null;
  /** The page types; null for all four. */
  readonly pageTypes: readonly string[] | null;
  /** The epoch written; null for the served one. */
  readonly epoch: number | null;
  /** Whether the units already complete in that epoch are left out. */
  readonly skipComplete: boolean;
  /**
   * `openers` for the patch page and the openers of the index alone (`OPENERS_PAGES`); every
   * page when absent or null.
   */
  readonly pages?: string | null;
  /** The pass it was prefilled from, which the summary names; null otherwise. */
  readonly fromPass: number | null;
}
