import type { ReleaseFeature } from './release-feature';
import type { ReleaseFix } from './release-fix';
import type { ReleaseType } from './release-type';

/**
 * One release of the player-facing changelog, read from `changelog/<id>.json`. The files are
 * written by the release tooling (`changelog/published/`), in French,
 * and shown as they are under every locale.
 */
export interface ChangelogRelease {
  readonly version: string;
  readonly codename: string;
  /** ISO calendar date, `yyyy-mm-dd`. */
  readonly date: string;
  readonly type: ReleaseType;
  /** One-line summary, shown on the collapsed release. */
  readonly summary: string;
  readonly intro: string | null;
  readonly features: readonly ReleaseFeature[];
  readonly fixes: readonly ReleaseFix[];
  readonly devNote: { readonly body: string; readonly signature: string | null } | null;
}
