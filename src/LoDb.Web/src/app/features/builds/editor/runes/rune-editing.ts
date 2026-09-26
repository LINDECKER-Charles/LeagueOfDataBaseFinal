import { Injectable, inject, linkedSignal } from '@angular/core';
import { EditorCatalogs } from '../catalog/editor-catalogs';
import { EDITOR_ENTRY } from '../form/editor-entry-token';
import { RuneDraft } from './rune-draft';

/**
 * The rune page of an editor. A stored page opens with its secondary runes on the ghost
 * row: their rows need the trees, and each list of trees read re-anchors them.
 */
@Injectable()
export class RuneEditing {
  private readonly stored = inject(EDITOR_ENTRY).draft.structure.runes;
  private readonly draft = linkedSignal({
    source: inject(EditorCatalogs).runes.options,
    computation: (trees, previous): RuneDraft => {
      const draft = previous?.value ?? RuneDraft.of(this.stored, () => null);
      return trees === null ? draft : draft.reanchored(trees);
    },
  });

  readonly page = this.draft.asReadonly();

  setPrimaryStyle(styleId: number): void {
    this.draft.update((draft) => draft.withPrimaryStyle(styleId));
  }

  setPrimaryPerk(slotIndex: number, perkId: number): void {
    this.draft.update((draft) => draft.withPrimaryPerk(slotIndex, perkId));
  }

  setSecondaryStyle(styleId: number): void {
    this.draft.update((draft) => draft.withSecondaryStyle(styleId));
  }

  setSecondaryPerk(slotIndex: number, perkId: number): void {
    this.draft.update((draft) => draft.withSecondaryPerk(slotIndex, perkId));
  }
}
