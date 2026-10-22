import { PendingTasks, effect, inject } from '@angular/core';
import { TranslocoService } from '@jsverse/transloco';
import { firstValueFrom } from 'rxjs';
import type { PatchChanges } from '../../../../core/api/generated/models/patch-changes';
import type { CatalogueEntry } from '../../../../core/routing/catalogue/catalogue-entry';
import { Seo } from '../../../../core/seo/seo';
import { CHANGES_SCOPE } from '../changes-scope';
import { patchChangesHeadOf } from './patch-changes-head-of';

/**
 * Writes the head of the page of a patch's changes once its catalogues are loaded, the root
 * one and the page's, and again when the router reuses the page for another patch or locale.
 * Called from the page's constructor: the server render waits for it.
 */
export function applyPatchChangesHead(entry: () => CatalogueEntry<PatchChanges>): void {
  const seo = inject(Seo);
  const transloco = inject(TranslocoService);
  const tasks = inject(PendingTasks);
  effect(() => {
    const current = entry();
    const { locale } = current.context;
    tasks.run(async () => {
      const catalogues = [locale, `${CHANGES_SCOPE}/${locale}`];
      // A catalogue that fails leaves its keys to the `en` fallback, never the page headless.
      await Promise.all(
        catalogues.map((path) => firstValueFrom(transloco.load(path)).catch(() => undefined)),
      );
      await seo.apply(
        patchChangesHeadOf(current, (key, params) => transloco.translate(key, params, locale)),
      );
    });
  });
}
