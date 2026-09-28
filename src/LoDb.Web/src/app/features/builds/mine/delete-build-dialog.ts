import { DIALOG_DATA, DialogRef } from '@angular/cdk/dialog';
import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { TranslocoPipe, provideTranslocoScope } from '@jsverse/transloco';
import { Button } from '../../../ui/controls/button';
import { DialogFrame } from '../../../ui/overlays/dialog-frame';
import { BUILDS_EDITOR_SCOPE } from '../editor/shared/builds-editor-scope';

/**
 * Asks before a build is deleted for good, naming it. It closes on true to delete, on
 * nothing otherwise. Opened at the root of the page, it provides its scope again.
 */
@Component({
  selector: 'lodb-delete-build-dialog',
  imports: [Button, DialogFrame, TranslocoPipe],
  providers: [provideTranslocoScope(BUILDS_EDITOR_SCOPE)],
  template: `
    <lodb-dialog
      [heading]="'buildsEditor.delete.title' | transloco"
      [headingId]="headingId"
      [closeLabel]="'buildsEditor.delete.cancel' | transloco"
    >
      <p class="font-beaufort text-lg tracking-wide text-gold-bright">{{ name }}</p>
      <p class="mt-2 text-sm text-text-muted">{{ 'build.list.delete_confirm' | transloco }}</p>
      <div class="mt-6 flex flex-wrap justify-end gap-3">
        <button lodbButton="ghost" type="button" (click)="ref.close()">
          {{ 'buildsEditor.delete.cancel' | transloco }}
        </button>
        <button lodbButton type="button" class="delete-confirm" (click)="ref.close(true)">
          {{ 'build.list.delete' | transloco }}
        </button>
      </div>
    </lodb-dialog>
  `,
  styles: `
    .delete-confirm {
      color: var(--color-danger-light);
      border-color: color-mix(in srgb, var(--color-danger) 60%, transparent);
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class DeleteBuildDialog {
  /** Id of the dialog's heading, which names it: the opener's `labelledBy`. */
  static readonly HEADING_ID = 'lodb-delete-build-heading';

  protected readonly ref = inject<DialogRef<boolean>>(DialogRef);
  /** The name of the build to delete. */
  protected readonly name = inject<string>(DIALOG_DATA);
  protected readonly headingId = DeleteBuildDialog.HEADING_ID;
}
