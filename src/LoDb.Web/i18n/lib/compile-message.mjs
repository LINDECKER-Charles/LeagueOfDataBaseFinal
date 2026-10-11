import parser from '@messageformat/parser';

// What MessageFormat accepted at runtime (transloco-messageformat with strictPluralKeys off):
// `en` messages complete every locale, whose plural categories they need not match.
const PARSE_OPTIONS = Object.freeze({ strict: false, strictPluralKeys: false });
// Transloco's own `{{ param }}` interpolation: the matcher of its default markers.
const TRANSLOCO_PARAMETER = /{{([^{}]*?)}}/g;
// Neutral text standing in for a Transloco parameter while a message is probed for ICU syntax.
const PARAMETER_PROBE = 'x';
const OTHER = 'other';
const EXACT_MARKER = /^=/;

function compileCases(token, plural) {
  const cases = {};
  for (const { key, tokens } of token.cases) {
    cases[key.replace(EXACT_MARKER, '')] = compileTokens(tokens, plural);
  }
  if (!Object.hasOwn(cases, OTHER)) {
    throw new Error(`{${token.arg}, ${token.type}} has no '${OTHER}' case`);
  }
  return cases;
}

// `plural` is the innermost plural around the token, which `#` stands for.
function compileToken(token, plural) {
  switch (token.type) {
    case 'content':
      return token.value;
    case 'argument':
      return ['arg', token.arg];
    case 'octothorpe':
      return plural === null ? '#' : ['#', plural.arg, plural.pluralOffset ?? 0];
    case 'select':
      return ['select', token.arg, compileCases(token, plural)];
    case 'plural':
    case 'selectordinal':
      return [token.type, token.arg, token.pluralOffset ?? 0, compileCases(token, token)];
    default:
      throw new Error(`{${token.arg}, ${token.key}} is a formatter the site does not render`);
  }
}

function compileTokens(tokens, plural) {
  const parts = [];
  for (const token of tokens) {
    const part = compileToken(token, plural);
    const last = parts.length - 1;
    if (typeof part === 'string' && typeof parts[last] === 'string') {
      parts[last] += part;
    } else {
      parts.push(part);
    }
  }
  return parts;
}

function isPlainText(tokens, source) {
  return (
    tokens.every((token) => token.type === 'content') &&
    tokens.map((t) => t.value).join('') === source
  );
}

// A message with Transloco parameters is Transloco's alone: no ICU syntax, no ICU quoting.
function checkedTranslocoMessage(source) {
  const probe = source.replace(TRANSLOCO_PARAMETER, PARAMETER_PROBE);
  if (!isPlainText(parser.parse(probe, PARSE_OPTIONS), probe)) {
    throw new Error('mixes {{ }} parameters with ICU syntax: write {name} arguments instead');
  }
  return source;
}

/**
 * Compiles a catalogue message for `PrecompiledTranspiler` (src/app/core/i18n/messages): the
 * text it renders when the message has no ICU syntax, otherwise its `CompiledMessage` parts.
 * Throws on a message MessageFormat would have refused, or one the runtime cannot render
 * (a formatter such as `{n, number}`), naming the reason.
 */
export function compileMessage(source) {
  if (source.search(TRANSLOCO_PARAMETER) >= 0) {
    return checkedTranslocoMessage(source);
  }
  const parts = compileTokens(parser.parse(source, PARSE_OPTIONS), null);
  if (parts.length === 0) {
    return '';
  }
  return parts.length === 1 && typeof parts[0] === 'string' ? parts[0] : parts;
}
