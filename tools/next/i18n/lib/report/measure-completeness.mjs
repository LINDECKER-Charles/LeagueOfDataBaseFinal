import { compileIcu } from '../compile-icu.mjs';

/**
 * Compares every locale of every scope with the reference locale, the runtime fallback:
 * keys it lacks (served in the reference language), keys the reference does not know
 * (never displayed), and messages MessageFormat rejects (a broken render).
 */
export function measureCompleteness({ locales, scopes }, referenceLocale) {
  return [...scopes].map(([scope, catalogues]) => {
    const reference = catalogues.get(referenceLocale);
    if (!reference) {
      throw new Error(`Scope "${scope || '(root)'}" has no ${referenceLocale} catalogue`);
    }
    return {
      scope,
      referenceKeys: [...reference.keys()],
      locales: locales.map((locale) => compareLocale(locale, catalogues.get(locale), reference)),
    };
  });
}

function compareLocale(locale, catalogue, reference) {
  const messages = catalogue ?? new Map();
  return {
    locale,
    fileMissing: catalogue === undefined,
    missing: [...reference.keys()].filter((key) => !messages.has(key)),
    extra: [...messages.keys()].filter((key) => !reference.has(key)),
    invalid: [...messages].flatMap(([key, message]) => invalidMessage(key, message, locale)),
  };
}

function invalidMessage(key, message, locale) {
  try {
    compileIcu(String(message), locale);
    return [];
  } catch (error) {
    return [{ key, error: error.message.split('\n')[0] }];
  }
}
