import { Pipe, type PipeTransform, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { TranslocoService } from '@jsverse/transloco';
import { map } from 'rxjs';
import { ADMIN_I18N } from './admin-i18n';

/** The values a text interpolates. */
type TextParams = Readonly<Record<string, unknown>>;

/**
 * `'admin.key' | adminText: params`: a text of the `admin` scope, in French whatever the
 * active language of the site, empty until the catalogue is loaded. The admin does not use
 * Transloco's own pipe: that one honours `provideTranslocoLang` for its first key only, then
 * follows the active language once its key or its parameters change (a status after an
 * action, the time of a new reading).
 */
@Pipe({ name: 'adminText', pure: false })
export class AdminTextPipe implements PipeTransform {
  private readonly transloco = inject(TranslocoService);
  // Read while rendering, so the view renders again once the catalogue arrives.
  private readonly loaded = toSignal(
    this.transloco.selectTranslation(ADMIN_I18N.path).pipe(map(() => true)),
    { initialValue: false },
  );
  private lastInput = '';
  private lastValue = '';

  transform(key: string | null | undefined, params?: TextParams): string {
    if (!this.loaded() || !key) {
      return '';
    }
    const input = params ? `${key}${JSON.stringify(params)}` : key;
    if (input !== this.lastInput) {
      this.lastInput = input;
      this.lastValue = this.transloco.translate<string>(key, params, ADMIN_I18N.lang);
    }
    return this.lastValue;
  }
}
