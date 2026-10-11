import { Injectable } from '@angular/core';
import { DefaultTranspiler, type TranspileParams } from '@jsverse/transloco';
import { DEFAULT_LOCALE } from '../default-locale';
import type { CompiledMessage } from './compiled-message';
import { formatCompiledMessage } from './format-compiled-message';
import { LocaleFormats } from './locale-formats';

function isCompiledMessage(value: unknown): value is CompiledMessage {
  return Array.isArray(value);
}

/**
 * Transloco's transpiler for the catalogues the build ships. A plain string keeps Transloco's
 * own `{{ param }}` interpolation and nothing else: most messages have no ICU syntax, and
 * MessageFormat parsed and compiled every one of them on every render. An ICU message comes
 * already compiled (`CompiledMessage`) and is only rendered: no message compiler reaches the
 * page, which is what lets its policy refuse `'unsafe-eval'`. Plural rules and number formats
 * follow the active language, as they did under MessageFormat.
 */
@Injectable()
export class PrecompiledTranspiler extends DefaultTranspiler {
  private formats = new LocaleFormats(DEFAULT_LOCALE);

  override transpile(params: TranspileParams): unknown {
    const { value } = params;
    return isCompiledMessage(value)
      ? formatCompiledMessage(value, params.params ?? {}, this.formats)
      : super.transpile(params);
  }

  onLangChanged(lang: string): void {
    if (lang !== this.formats.locale) {
      this.formats = new LocaleFormats(lang);
    }
  }
}
