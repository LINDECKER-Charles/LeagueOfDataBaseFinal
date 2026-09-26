import type { BuildDraft } from '../../../core/api/generated/models/build-draft';
import type { GameMode } from '../../../core/api/generated/models/game-mode';
import type { ImportReport } from '../../../core/api/generated/models/import-report';

/**
 * What the editor opens on, resolved before it renders: a blank build, an owned build to
 * edit, or an owned build carried over to another patch, then created as a new build.
 */
export interface EditorEntry {
  /** `create` posts a new build (a blank one or an import), `edit` saves the build edited. */
  readonly mode: 'create' | 'edit';
  /** The build edited, or the source of an import; null for a blank build. */
  readonly buildId: number | null;
  readonly draft: BuildDraft;
  /** What an import could not carry over; null outside an import. */
  readonly report: ImportReport | null;
  /** The Data Dragon language the pickers and the refusals name things in: the browsed one. */
  readonly lang: string;
  /** The patches the version select offers, the latest first. */
  readonly versions: readonly string[];
  /** The Data Dragon languages a build may be written in. */
  readonly languages: readonly string[];
  readonly gameModes: readonly GameMode[];
}
