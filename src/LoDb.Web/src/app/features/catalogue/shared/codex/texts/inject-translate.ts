import { type Signal, computed, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { TranslocoService } from '@jsverse/transloco';
import type { Translate } from './translate';

/**
 * The active locale's translation as a signal of {@link Translate}: a computed reading it,
 * such as a list's facet schema, is rebuilt when the locale or its catalogue changes.
 */
export function injectTranslate(): Signal<Translate> {
  const transloco = inject(TranslocoService);
  const translation = toSignal(transloco.selectTranslation(), { initialValue: {} });
  return computed(() => {
    translation();
    return (key, params) => transloco.translate(key, params);
  });
}
