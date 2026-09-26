import { requireWebPackage } from './require-web-package.mjs';

const MessageFormat = requireWebPackage('@messageformat/core');
// Same options as provideI18n() (src/app/core/i18n): `en` messages render under every
// locale, and ja, ko, th, vi or zh have no `one` category to accept them strictly.
const OPTIONS = { strictPluralKeys: false };
const formatters = new Map();

function formatterFor(locale) {
  if (!formatters.has(locale)) {
    formatters.set(locale, new MessageFormat(locale, OPTIONS));
  }
  return formatters.get(locale);
}

/**
 * Compiles a catalogue message the way `transloco-messageformat` does at runtime, and
 * throws on the syntax errors that would otherwise surface in the middle of a render.
 * Transloco substitutes `{{ param }}` before MessageFormat sees the message: blanking them
 * here mirrors that order.
 */
export function compileIcu(message, locale) {
  return formatterFor(locale).compile(message.replace(/{{\s*[^{}]*?\s*}}/g, ''));
}
