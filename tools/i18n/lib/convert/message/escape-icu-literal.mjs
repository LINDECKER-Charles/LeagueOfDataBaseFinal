const APOSTROPHE = "'";
const HASH = '#';

/**
 * Escapes literal text for MessageFormat, which Transloco runs on every message.
 * `{`, `}` (and `#` inside a plural branch) are quoted together with any apostrophe touching
 * them, so no two quotes ever meet. A lone apostrophe stays readable (`l'objet`) unless it
 * could open a quote: before syntax, or before `#`, which MessageFormat lexes as a quote
 * opener even outside plurals. `followedBySyntax` says what comes right after the text: an
 * argument, or the brace closing a plural branch.
 */
export function escapeIcuLiteral(text, { inPlural = false, followedBySyntax = false } = {}) {
  const isSyntax = (char) => char === '{' || char === '}' || (inPlural && char === HASH);
  const isSpecial = (char) => char === APOSTROPHE || isSyntax(char);
  let escaped = '';
  let index = 0;
  while (index < text.length) {
    if (!isSpecial(text[index])) {
      escaped += text[index];
      index += 1;
      continue;
    }
    let end = index;
    while (end < text.length && isSpecial(text[end])) {
      end += 1;
    }
    const run = text.slice(index, end);
    const next = end === text.length ? (followedBySyntax ? '{' : '') : text[end];
    escaped += escapeRun(run, [...run].some(isSyntax), next);
    index = end;
  }
  return escaped;
}

function escapeRun(run, hasSyntax, next) {
  const doubled = run.replaceAll(APOSTROPHE, APOSTROPHE + APOSTROPHE);
  if (hasSyntax) {
    return `${APOSTROPHE}${doubled}${APOSTROPHE}`;
  }
  return run.length === 1 && next !== '{' && next !== HASH ? run : doubled;
}
