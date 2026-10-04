import { Injectable, inject, signal } from '@angular/core';
import type { GameMode } from '../../../../core/api/generated/models/game-mode';
import { EDITOR_ENTRY } from './editor-entry-token';

/**
 * The fields of the build being edited but its runes and its purchase order, which have
 * their own rules: its identity, the context it is written in, its champion. The patch and
 * the mode choose the picker lists; the language only says what the free text is written in.
 */
@Injectable()
export class BuildEditorStore {
  private readonly draft = inject(EDITOR_ENTRY).draft;

  readonly name = signal(this.draft.name);
  readonly description = signal(this.draft.description ?? '');
  readonly isPublic = signal(this.draft.isPublic);
  readonly gameVersion = signal(this.draft.gameVersion);
  readonly gameMode = signal<GameMode>(this.draft.gameMode);
  readonly language = signal(this.draft.language);
  /** A Data Dragon champion id; empty until one is picked. */
  readonly championId = signal(this.draft.structure.championId);
}
