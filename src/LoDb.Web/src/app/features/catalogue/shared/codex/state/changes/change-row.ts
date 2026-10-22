import type { SegmentKind } from '../../../../../../core/api/generated/models/segment-kind';

/**
 * A changed field as the block shows it: its label, then plain-text runs, each kept, struck
 * out (`<del>`) or inserted (`<ins>`), and the arrow between a value before and after, a
 * value's pieces spaced by kept runs. Never HTML: Riot's text is written as text.
 */
export interface ChangeRow {
  readonly label: string;
  /** A text's diff, whose line breaks show; values otherwise. */
  readonly isText: boolean;
  readonly pieces: readonly { readonly kind: SegmentKind | 'arrow'; readonly text: string }[];
}
