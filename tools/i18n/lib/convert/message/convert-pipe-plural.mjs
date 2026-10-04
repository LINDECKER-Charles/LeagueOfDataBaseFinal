import { buildIcuPlural } from './build-icu-plural.mjs';

// Symfony's own grammar (Translator, MessageSelector): `||` is a literal pipe, a segment may
// start with an explicit set `{0}`, an interval `]1,Inf[` or an ignored label `one:`.
const SEGMENT_SEPARATOR = /(?<!\|)\|(?!\|)/;
const EXPLICIT_SET = /^\s*\{\s*([-+]?\d+(?:\s*,\s*[-+]?\d+)*)\s*\}\s*([\s\S]*)$/;
const INTERVAL = /^\s*([[\]])\s*(-Inf|[-+]?\d+)\s*,\s*(\+?Inf|[-+]?\d+)\s*([[\]])\s*([\s\S]*)$/;
const LABEL = /^\s*\w+:\s*([\s\S]*)$/;
const CLDR_ORDER = ['zero', 'one', 'two', 'few', 'many', 'other'];

/**
 * Converts a Symfony pipe plural (`one apple|%count% apples`) into an ICU plural. Unlabelled
 * segments take the locale's CLDR categories in order, the last one becoming `other`;
 * explicit sets become `=n`, unbounded intervals `other`. Any other interval has no ICU
 * equivalent and fails the conversion with the message, rather than silently changing it.
 */
export function convertPipePlural(source, locale) {
  const segments = source.split(SEGMENT_SEPARATOR).map((segment) => segment.replaceAll('||', '|'));
  const categories = orderedCategories(locale);
  const unlabelled = segments.filter((segment) => !isExplicit(segment)).length;
  let position = 0;
  const branches = segments.flatMap((segment) => {
    const explicit = explicitBranches(segment, source);
    if (explicit) {
      return explicit;
    }
    position += 1;
    const selector = position === unlabelled ? 'other' : (categories[position - 1] ?? 'other');
    return [{ selector, text: segment.replace(LABEL, '$1') }];
  });
  return buildIcuPlural(branches);
}

function orderedCategories(locale) {
  const categories = new Intl.PluralRules(locale).resolvedOptions().pluralCategories;
  return CLDR_ORDER.filter((category) => category !== 'other' && categories.includes(category));
}

function isExplicit(segment) {
  return EXPLICIT_SET.test(segment) || INTERVAL.test(segment);
}

function explicitBranches(segment, source) {
  const set = EXPLICIT_SET.exec(segment);
  if (set) {
    return set[1].split(',').map((value) => ({ selector: `=${Number(value)}`, text: set[2] }));
  }
  const interval = INTERVAL.exec(segment);
  return interval ? [{ selector: intervalSelector(interval, source), text: interval[5] }] : null;
}

function intervalSelector([, open, low, high, close], source) {
  if (/Inf$/.test(high)) {
    return 'other';
  }
  if (open === '[' && close === ']' && Number(low) === Number(high)) {
    return `=${Number(low)}`;
  }
  throw new Error(`Interval ${open}${low},${high}${close} has no ICU equivalent: ${source}`);
}
