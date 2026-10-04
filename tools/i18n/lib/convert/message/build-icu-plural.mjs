import { PLURAL_VARIABLE } from './plural-variable.mjs';
import { renderMessage } from './render-message.mjs';
import { tokenizeMessage } from './tokenize-message.mjs';

const OTHER = 'other';

/**
 * Builds `{count, plural, <selector> {…} … other {…}}` from `{ selector, text }` branches
 * whose text is still in Symfony syntax. ICU requires `other`: without one, the last
 * branch also serves as `other`, the Symfony behaviour for counts no rule matches.
 */
export function buildIcuPlural(branches) {
  const complete = branches.some((branch) => branch.selector === OTHER)
    ? branches
    : [...branches, { selector: OTHER, text: branches.at(-1).text }];
  const rendered = complete.map(({ selector, text }) => {
    return `${selector} {${renderMessage(tokenizeMessage(text), { inPlural: true })}}`;
  });
  return `{${PLURAL_VARIABLE}, plural, ${rendered.join(' ')}}`;
}
