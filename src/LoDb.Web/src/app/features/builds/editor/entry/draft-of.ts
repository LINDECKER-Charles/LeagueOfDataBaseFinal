import type { BuildDraft } from '../../../../core/api/generated/models/build-draft';
import type { EditableBuild } from '../../../../core/api/generated/models/editable-build';

/** The fields of an owned build the editor fills its form with. */
export function draftOf(build: EditableBuild): BuildDraft {
  return {
    name: build.name,
    description: build.description ?? null,
    isPublic: build.isPublic,
    gameVersion: build.gameVersion,
    gameMode: build.gameMode,
    language: build.language,
    structure: build.structure,
  };
}
