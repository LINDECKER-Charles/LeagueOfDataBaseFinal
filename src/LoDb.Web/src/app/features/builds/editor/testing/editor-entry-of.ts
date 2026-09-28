import type { BuildStructure } from '../../../../core/api/generated/models/build-structure';
import type { EditorEntry } from '../editor-entry';

const BLANK_STRUCTURE: BuildStructure = {
  championId: '',
  runes: { primaryStyleId: 0, primarySelections: [], secondaryStyleId: 0, secondarySelections: [] },
  steps: [],
};

/** An entry for the specs: a blank build on 16.19.1, with what a spec says in place. */
export function editorEntryOf(
  structure: Partial<BuildStructure> = {},
  entry: Partial<EditorEntry> = {},
): EditorEntry {
  return {
    mode: 'create',
    buildId: null,
    report: null,
    lang: 'en_US',
    versions: ['16.19.1', '15.14.1'],
    languages: ['en_US', 'fr_FR'],
    gameModes: ['sr', 'aram'],
    draft: {
      name: '',
      description: null,
      isPublic: false,
      gameVersion: '16.19.1',
      gameMode: 'sr',
      language: 'en_US',
      structure: { ...BLANK_STRUCTURE, ...structure },
    },
    ...entry,
  };
}
