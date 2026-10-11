import type { PatchRow } from './patch-row';

/** The versions sharing a first segment, such as every `16.x`, newest first. */
export interface PatchGroup {
  /** The first segment of its versions, such as `16`. */
  readonly major: string;
  readonly rows: readonly PatchRow[];
}
