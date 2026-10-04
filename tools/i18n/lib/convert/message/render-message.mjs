import { escapeIcuLiteral } from './escape-icu-literal.mjs';
import { PLURAL_VARIABLE } from './plural-variable.mjs';

/**
 * Writes tokens in the Transloco + MessageFormat syntax. Outside a plural, `%param%` becomes
 * `{{ param }}` (Transloco interpolation). Inside a plural branch the count becomes `#` and
 * other parameters ICU arguments: Transloco splices `{{ }}` values in before MessageFormat
 * parses the message, so a `#` in such a value would turn into the count.
 */
export function renderMessage(tokens, { inPlural = false } = {}) {
  return tokens
    .map((token, index) => {
      if (token.kind === 'param') {
        return renderParam(token.name, inPlural);
      }
      const followedBySyntax = index < tokens.length - 1 || inPlural;
      return escapeIcuLiteral(token.text, { inPlural, followedBySyntax });
    })
    .join('');
}

function renderParam(name, inPlural) {
  if (!inPlural) {
    return `{{ ${name} }}`;
  }
  return name === PLURAL_VARIABLE ? '#' : `{${name}}`;
}
