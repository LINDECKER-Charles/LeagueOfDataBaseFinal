import { Injectable } from '@angular/core';
import { DefaultTranspiler, type TranspileParams } from '@jsverse/transloco';
import { DEFAULT_LOCALE } from '../default-locale';
import type { CompiledMessage } from './compiled-message';
import { formatCompiledMessage } from './format-compiled-message';
import { LocaleFormats } from './locale-formats';

// Scopes written in one language whatever the site's: their plurals and numbers follow that
// language, never the active one. The admin is in French alone (features/admin/shared).
const SINGLE_LANGUAGE_SCOPES: Readonly<Record<string, string>> = { admin: 'fr' };

function isCompiledMessage(value: unknown): value is CompiledMessage {
  return Array.isArray(value);
}

// The scope of a key Transloco resolved: `admin.prerender.title` is in `admin`.
function scopeOf(key: string | undefined): string {
  return key?.split('.', 1)[0] ?? '';
}

/**
 * Transloco's transpiler for the catalogues the build ships. A plain string keeps Transloco's
 * own `{{ param }}` interpolation and nothing else: most messages have no ICU syntax, and
 * MessageFormat parsed and compiled every one of them on every render. An ICU message comes
 * already compiled (`CompiledMessage`) and is only rendered: no message compiler reaches the
 * page, which is what lets its policy refuse `'unsafe-eval'`. Plural rules and number formats
 * follow the active language, as they did under MessageFormat, except in a scope written in
 * one language only (the admin), whose messages follow that language.
 */
@Injectable()
export class PrecompiledTranspiler extends DefaultTranspiler {
  private formats = new LocaleFormats(DEFAULT_LOCALE);
  private readonly scopeFormats = new Map<string, LocaleFormats>();

  override transpile(params: TranspileParams): unknown {
    const { value } = params;
    return isCompiledMessage(value)
      ? formatCompiledMessage(value, params.params ?? {}, this.formatsOf(params.key))
      : super.transpile(params);
  }

  onLangChanged(lang: string): void {
    if (lang !== this.formats.locale) {
      this.formats = new LocaleFormats(lang);
    }
  }

  private formatsOf(key: string | undefined): LocaleFormats {
    const scope = scopeOf(key);
    const locale = Object.hasOwn(SINGLE_LANGUAGE_SCOPES, scope)
      ? SINGLE_LANGUAGE_SCOPES[scope]
      : undefined;
    if (locale === undefined) {
      return this.formats;
    }
    let formats = this.scopeFormats.get(locale);
    if (formats === undefined) {
      formats = new LocaleFormats(locale);
      this.scopeFormats.set(locale, formats);
    }
    return formats;
  }
}
