import { DOCUMENT, Injectable, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Directionality } from '@angular/cdk/bidi';
import { TranslocoService } from '@jsverse/transloco';
import { filter } from 'rxjs';
import { DEFAULT_LOCALE } from '../../i18n/default-locale';
import { isLocale } from '../../i18n/is-locale';
import type { Locale } from '../../i18n/locales';
import { textDirection } from './text-direction';

/**
 * Locale and writing direction of the page. It starts from `<html lang>`, which the SSR
 * output already carries, rather than from Transloco, whose active language reads `en` until
 * the route resolver runs: an Arabic page never flashes left-to-right while it hydrates. It
 * then follows every language change, and sets `<html dir>` and the CDK's root
 * Directionality together so overlays mirror with the page.
 */
@Injectable({ providedIn: 'root' })
export class PageDirection {
  private readonly document = inject(DOCUMENT);
  private readonly directionality = inject(Directionality);
  private readonly activeLocale = signal<Locale>(this.localeOfDocument());

  readonly locale = this.activeLocale.asReadonly();
  readonly direction = computed(() => textDirection(this.activeLocale()));

  constructor() {
    this.apply(this.activeLocale());
    inject(TranslocoService)
      .events$.pipe(
        filter((event) => event.type === 'langChanged'),
        takeUntilDestroyed(),
      )
      .subscribe((event) => this.apply(event.payload.langName));
  }

  private localeOfDocument(): Locale {
    const lang = this.document.documentElement.lang;
    return isLocale(lang) ? lang : DEFAULT_LOCALE;
  }

  private apply(requested: string): void {
    if (!isLocale(requested)) {
      return;
    }
    const direction = textDirection(requested);
    this.activeLocale.set(requested);
    this.document.documentElement.dir = direction;
    if (this.directionality.value !== direction) {
      this.directionality.valueSignal.set(direction);
      this.directionality.change.emit(direction);
    }
  }
}
