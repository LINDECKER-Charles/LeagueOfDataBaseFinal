/** A feature card of a release. */
export interface ReleaseFeature {
  /** `mana` draws the card in blue; anything else in gold. */
  readonly kind: 'feature' | 'mana';
  readonly glyph: string;
  readonly tag: string | null;
  readonly title: string;
  readonly description: string;
}
