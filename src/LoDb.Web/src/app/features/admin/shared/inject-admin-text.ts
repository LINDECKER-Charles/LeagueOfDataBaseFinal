import { inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { TranslocoService } from '@jsverse/transloco';
import { ADMIN_I18N } from './admin-i18n';

/** The values a text interpolates. */
type TextParams = Readonly<Record<string, unknown>>;

/** The texts of the admin read from code, for the labels a chart draws. */
export interface AdminText {
  /** The text of `key`, relative to the `admin` scope. */
  text(key: string, params?: TextParams): string;
  /** The name of a term of the API's vocabulary (`group.name`), the term itself when unknown. */
  term(group: string, name: string): string;
}

/**
 * Translates keys of the `admin` scope from code, in French. Reading them inside a
 * `computed` makes it compute again once the catalogue arrives.
 */
export function injectAdminText(): AdminText {
  const transloco = inject(TranslocoService);
  const catalogue = toSignal(transloco.selectTranslation(ADMIN_I18N.path), { initialValue: {} });
  const text = (key: string, params?: TextParams) => {
    catalogue();
    return transloco.translate<string>(key, params, ADMIN_I18N.path);
  };
  return {
    text,
    term: (group, name) => {
      const key = `${group}.${name}`;
      return key in catalogue() ? text(key) : name;
    },
  };
}
