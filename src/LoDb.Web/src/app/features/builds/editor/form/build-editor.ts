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
import { ImportNotices } from '../report/import-notices';
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
 * new editor. The API checks the build and names what it refuses, shown over the save.
 */
@Component({
  selector: 'lodb-build-editor',
  imports: [
    Button,
    ChampionPicker,
    EditorContext,
    Field,
    ImportNotices,
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
  protected readonly listLink = localePath(inject(PageDirection).locale(), 'account/builds');

  protected submit(event: Event): void {
    event.preventDefault();
    void this.saver.save();
  }
}
