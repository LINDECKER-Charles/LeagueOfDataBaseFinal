import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { TranslocoPipe } from '@jsverse/transloco';
import { languageName } from '../../../../core/api/meta/language-name';
import { Field } from '../../../../ui/controls/field';
import { BuildEditorStore } from '../form/build-editor-store';
import { EDITOR_ENTRY } from '../form/editor-entry-token';
import { versionChoices } from './version-choices';

/**
 * The game context of the build: its patch and its mode, which choose the picker lists, and
 * the language its text is written in. A switch never drops what is placed: the pickers
 * flag what the new context lacks.
 */
@Component({
  selector: 'lodb-editor-context',
  imports: [Field, TranslocoPipe],
  template: `
    <div class="grid gap-4 sm:grid-cols-3">
      <label class="flex flex-col gap-1.5 text-sm">
        <span class="text-text-muted">{{ 'build.editor.context.version' | transloco }}</span>
        <select #version lodbField (change)="store.gameVersion.set(version.value)">
          @for (choice of versions; track choice) {
            <option [value]="choice" [selected]="choice === store.gameVersion()">
              {{ choice }}
            </option>
          }
        </select>
      </label>
      <label class="flex flex-col gap-1.5 text-sm">
        <span class="text-text-muted">{{ 'build.editor.context.mode' | transloco }}</span>
        <select #mode lodbField (change)="setMode(mode.value)">
          @for (choice of modes; track choice) {
            <option [value]="choice" [selected]="choice === store.gameMode()">
              {{ 'build.mode.' + choice | transloco }}
            </option>
          }
        </select>
      </label>
      <label class="flex flex-col gap-1.5 text-sm">
        <span class="text-text-muted">{{ 'build.editor.context.language' | transloco }}</span>
        <select #language lodbField (change)="store.language.set(language.value)">
          @for (choice of languages; track choice.code) {
            <option [value]="choice.code" [selected]="choice.code === store.language()">
              {{ choice.label }}
            </option>
          }
        </select>
      </label>
    </div>
    <p class="mt-3 font-mono text-xs text-text-dim">
      {{ 'build.editor.context.mode_hint' | transloco }}
    </p>
  `,
  host: { class: 'block' },
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class EditorContext {
  protected readonly store = inject(BuildEditorStore);
  private readonly entry = inject(EDITOR_ENTRY);
  protected readonly modes = this.entry.gameModes;
  protected readonly languages = this.entry.languages.map((code) => ({
    code,
    label: languageName(code),
  }));
  protected readonly versions = versionChoices(this.entry.versions, this.entry.draft.gameVersion);

  protected setMode(mode: string): void {
    const known = this.modes.find((choice) => choice === mode);
    if (known) {
      this.store.gameMode.set(known);
    }
  }
}
