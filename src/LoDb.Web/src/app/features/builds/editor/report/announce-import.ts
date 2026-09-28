import { inject } from '@angular/core';
import { TranslocoService } from '@jsverse/transloco';
import { ToastService } from '../../../../core/layout/toast/toast-service';
import { EDITOR_ENTRY } from '../form/editor-entry-token';
import { reportNoticesOf } from './report-notices-of';

/**
 * Toasts what an import changed, once, as the legacy editor flashed it: the patch it landed
 * on, then each thing to review before saving. Nothing outside an import. Runs in the
 * injection context of an editor, which only a browser opens.
 */
export function announceImport(): void {
  const entry = inject(EDITOR_ENTRY);
  if (entry.report === null) {
    return;
  }
  const toasts = inject(ToastService);
  const transloco = inject(TranslocoService);
  reportNoticesOf(entry.report, entry.draft.gameVersion).forEach((notice, index) => {
    const kind = index === 0 ? 'success' : 'warning';
    toasts.show(kind, transloco.translate(notice.key, notice.params));
  });
}
