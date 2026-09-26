import { requireWebPackage } from '../../lib/require-web-package.mjs';

const MessageFormat = requireWebPackage('@messageformat/core');
// Transloco's own matcher (resolveMatcher): the name cannot contain a brace.
const INTERPOLATION = /{{([^{}]*?)}}/g;

/**
 * What `translate()` returns with `transloco-messageformat`: Transloco substitutes
 * `{{ param }}` (missing ones become ''), then MessageFormat formats the result.
 */
export function renderLikeTransloco(message, params, locale) {
  const interpolated = message.replace(INTERPOLATION, (_, name) =>
    String(params[name.trim()] ?? ''),
  );
  return new MessageFormat(locale, { strictPluralKeys: false }).compile(interpolated)(params);
}
