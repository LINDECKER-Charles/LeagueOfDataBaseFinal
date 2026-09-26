import { Injectable, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { TranslocoService } from '@jsverse/transloco';
import { type Observable, firstValueFrom } from 'rxjs';
import type { EditableBuild } from '../../../../core/api/generated/models/editable-build';
import { BuildsService } from '../../../../core/api/generated/services/builds.service';
import { ToastService } from '../../../../core/layout/toast/toast-service';
import { BuildEditorStore } from '../form/build-editor-store';
import { EDITOR_ENTRY } from '../form/editor-entry-token';
import { RuneEditing } from '../runes/rune-editing';
import type { EditorMessage } from '../shared/editor-message';
import { StepEditing } from '../steps/step-editing';
import { buildProblem } from './build-problem';
import { buildRequestOf } from './build-request-of';
import { saveMessagesOf } from './save-messages-of';

/**
 * Saves the build of an editor: a new build is created, an owned one replaced. A saved build
 * opens on its share page; a refused one keeps the form as typed, with the API's messages
 * over it, shown as the API worded them.
 */
@Injectable()
export class BuildSaver {
  private readonly builds = inject(BuildsService);
  private readonly entry = inject(EDITOR_ENTRY);
  private readonly store = inject(BuildEditorStore);
  private readonly runes = inject(RuneEditing);
  private readonly steps = inject(StepEditing);
  private readonly router = inject(Router);
  private readonly toasts = inject(ToastService);
  private readonly transloco = inject(TranslocoService);

  readonly saving = signal(false);
  readonly messages = signal<readonly EditorMessage[]>([]);

  async save(): Promise<void> {
    if (this.saving()) {
      return;
    }
    this.saving.set(true);
    this.messages.set([]);
    try {
      const build = await firstValueFrom(this.send());
      const flash = this.entry.mode === 'edit' ? 'build.flash.updated' : 'build.flash.created';
      this.toasts.show('success', this.transloco.translate(flash));
      await this.router.navigateByUrl(`/b/${build.shareToken}`);
    } catch (error) {
      this.messages.set(saveMessagesOf(buildProblem(error)));
    } finally {
      this.saving.set(false);
    }
  }

  private send(): Observable<EditableBuild> {
    const body = buildRequestOf({
      store: this.store,
      runes: this.runes.page(),
      order: this.steps.order(),
    });
    const { lang, buildId } = this.entry;
    return this.entry.mode === 'edit' && buildId !== null
      ? this.builds.updateBuild({ id: buildId, lang, body })
      : this.builds.createBuild({ lang, body });
  }
}
