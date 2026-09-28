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
    <div class="forge-context">
      <label class="forge-context__field">
        <span class="forge-context__label">{{ 'build.editor.context.version' | transloco }}</span>
        <select #version lodbField (change)="store.gameVersion.set(version.value)">
          @for (choice of versions; track choice) {
            <option [value]="choice" [selected]="choice === store.gameVersion()">
              {{ choice }}
            </option>
          }
        </select>
      </label>
      <label class="forge-context__field">
        <span class="forge-context__label">{{ 'build.editor.context.mode' | transloco }}</span>
        <select #mode lodbField (change)="setMode(mode.value)">
          @for (choice of modes; track choice) {
            <option [value]="choice" [selected]="choice === store.gameMode()">
              {{ 'build.mode.' + choice | transloco }}
            </option>
          }
        </select>
      </label>
      <label class="forge-context__field">
        <span class="forge-context__label">{{ 'build.editor.context.language' | transloco }}</span>
        <select #language lodbField (change)="store.language.set(language.value)">
          @for (choice of languages; track choice.code) {
            <option [value]="choice.code" [selected]="choice.code === store.language()">
              {{ choice.label }}
            </option>
          }
        </select>
      </label>
    </div>
    <p class="mt-3 font-mono text-[0.7rem] tracking-[0.06em] text-text-dim">
      {{ 'build.editor.context.mode_hint' | transloco }}
    </p>
  `,
  // The legacy grid: the patch and the mode side by side, then the language, in columns
  // narrower than the section; labels in the eyebrow's voice, one size down (`.auth-label`).
  styles: `
    @layer components {
      .forge-context {
        display: grid;
        grid-template-columns: 1fr;
        gap: 1rem 1.25rem;
      }
      @media (width >= 40rem) {
        .forge-context {
          grid-template-columns: minmax(0, 14rem) minmax(0, 18rem);
        }
      }
      .forge-context__field {
        display: flex;
        flex-direction: column;
        gap: 0.375rem;
      }
      /*
       * A percentage width, as the legacy w-full: the widest option no longer sets the
       * column's minimum, which pushed the selects through the section's padding at 320 px.
       */
      .forge-context__field > select {
        inline-size: 100%;
      }
      .forge-context__label {
        font-family: var(--font-beaufort);
        font-size: 0.65rem;
        text-transform: uppercase;
        letter-spacing: 0.22em;
        color: var(--color-gold-light);
      }
    }
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
