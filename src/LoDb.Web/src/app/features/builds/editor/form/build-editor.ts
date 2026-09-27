import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { PageDirection } from '../../../../core/layout/direction/page-direction';
import { localePath } from '../../../../core/layout/shell/locale-path';
import { Button } from '../../../../ui/controls/button';
import { Field } from '../../../../ui/controls/field';
import { EditorCatalogs } from '../catalog/editor-catalogs';
import { ChampionPicker } from '../champion/champion-picker';
import { EditorContext } from '../context/editor-context';
import type { EditorEntry } from '../editor-entry';
import { announceImport } from '../report/announce-import';
import { RuneBoard } from '../runes/rune-board';
import { RuneEditing } from '../runes/rune-editing';
import { BuildSaver } from '../save/build-saver';
import { EditorAnnouncer } from '../steps/editor-announcer';
import { StepEditing } from '../steps/step-editing';
import { StepEditor } from '../steps/step-editor';
import { BuildEditorStore } from './build-editor-store';
import { EDITOR_ENTRY } from './editor-entry-token';

// The entry the route resolved, which every service of this editor starts from.
function routeEntry(): EditorEntry {
  return inject(ActivatedRoute).snapshot.data['entry'] as EditorEntry;
}

/**
 * The build editor: its identity, its game context, its champion, its runes and its purchase
 * order, then the save. Each editor holds its own state, provided here: a new entry makes a
 * new editor. The browser checks the name before anything is sent, as the legacy form did;
 * the API checks the rest and names what it refuses, in toasts. An import toasts its report.
 */
@Component({
  selector: 'lodb-build-editor',
  imports: [
    Button,
    ChampionPicker,
    EditorContext,
    Field,
    RouterLink,
    RuneBoard,
    StepEditor,
    TranslocoPipe,
  ],
  templateUrl: './build-editor.html',
  styleUrl: './build-editor.css',
  providers: [
    { provide: EDITOR_ENTRY, useFactory: routeEntry },
    BuildEditorStore,
    EditorCatalogs,
    EditorAnnouncer,
    RuneEditing,
    StepEditing,
    BuildSaver,
  ],
  host: { class: 'block' },
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class BuildEditor {
  protected readonly store = inject(BuildEditorStore);
  protected readonly saver = inject(BuildSaver);
  protected readonly isEdit = inject(EDITOR_ENTRY).mode === 'edit';
  /**
   * The name field's value, written once: the field alone changes the name afterwards. Written
   * again on each keystroke, it would no longer count as typed, and `minlength` would never
   * stop a short name (the browser only checks the length of what the user typed).
   */
  protected readonly initialName = this.store.name();
  protected readonly listLink = localePath(inject(PageDirection).locale(), 'account/builds');

  constructor() {
    announceImport();
  }

  protected submit(event: Event): void {
    event.preventDefault();
    void this.saver.save();
  }
}
