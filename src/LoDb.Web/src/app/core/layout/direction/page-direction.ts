import { DOCUMENT, Injectable, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Directionality } from '@angular/cdk/bidi';
import { NavigationEnd, Router } from '@angular/router';
import { filter } from 'rxjs';
import { DEFAULT_LOCALE } from '../../i18n/default-locale';
import { isLocale } from '../../i18n/is-locale';
import type { Locale } from '../../i18n/locales';
import { textDirection } from './text-direction';

/**
 * Locale and writing direction of the page, read from `<html lang>`: the locale resolver
 * writes the URL's locale there, and the SSR output already carries it, so an Arabic page
 * never flashes left-to-right while it hydrates. It is read again after every navigation,
 * and `<html dir>` and the CDK's root Directionality are set together so overlays mirror
 * with the page.
 *
 * Transloco's active language is not the source: after a catalogue fails to load, Transloco
 * switches to its fallback language, and an Arabic page with English text is still an Arabic
 * page, laid out right to left.
 */
@Injectable({ providedIn: 'root' })
export class PageDirection {
  private readonly document = inject(DOCUMENT);
  private readonly directionality = inject(Directionality);
  private readonly activeLocale = signal<Locale>(DEFAULT_LOCALE);

  readonly locale = this.activeLocale.asReadonly();
  readonly direction = computed(() => textDirection(this.activeLocale()));

  constructor() {
    this.apply(this.document.documentElement.lang);
    inject(Router)
      .events.pipe(
        filter((event) => event instanceof NavigationEnd),
        takeUntilDestroyed(),
      )
      .subscribe(() => this.apply(this.document.documentElement.lang));
  }

  private apply(lang: string): void {
    const locale = isLocale(lang) ? lang : DEFAULT_LOCALE;
    const direction = textDirection(locale);
    this.activeLocale.set(locale);
    this.document.documentElement.dir = direction;
    if (this.directionality.value !== direction) {
      this.directionality.valueSignal.set(direction);
      this.directionality.change.emit(direction);
    }
  }
}
