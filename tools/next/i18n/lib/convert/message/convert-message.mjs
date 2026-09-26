import { convertPipePlural } from './convert-pipe-plural.mjs';
import { PLURAL_VARIABLE } from './plural-variable.mjs';
import { renderMessage } from './render-message.mjs';
import { tokenizeMessage } from './tokenize-message.mjs';

const SINGLE_PIPE = /(?<!\|)\|(?!\|)/;

/**
 * Converts one Symfony message. Like Symfony, a pipe only means "plural" when the message
 * takes `%count%`: elsewhere it is plain text. `locale` is a BCP 47 tag (`zh-Hans`), which
 * the plural categories depend on.
 */
export function convertMessage(source, locale) {
  const pluralPlaceholder = `%${PLURAL_VARIABLE}%`;
  if (source.includes(pluralPlaceholder) && SINGLE_PIPE.test(source)) {
    return { kind: 'pipe-plural', message: convertPipePlural(source, locale) };
  }
  return { kind: 'plain', message: renderMessage(tokenizeMessage(source)) };
}
