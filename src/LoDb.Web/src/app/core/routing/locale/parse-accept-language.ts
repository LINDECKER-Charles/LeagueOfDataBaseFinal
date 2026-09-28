interface LanguageRange {
  readonly tag: string;
  readonly quality: number;
  /** Position in the header, which breaks ties between equal qualities. */
  readonly index: number;
}

const RANGE_SEPARATOR = ',';
const PARAMETER_SEPARATOR = ';';
const QUALITY_PREFIX = 'q=';
const DEFAULT_QUALITY = 1;
// RFC 9110: a weight is 0 to 1 with at most three decimals.
const QUALITY = /^(?:0(?:\.\d{0,3})?|1(?:\.0{0,3})?)$/;
// RFC 4647 basic language range; the wildcard `*` names no language and is skipped.
const LANGUAGE_TAG = /^[a-z]{1,8}(?:-[a-z\d]{1,8})*$/i;
// The header comes from anyone: past this many ranges, the rest is not worth reading.
const MAX_RANGES = 50;

function qualityOf(parameters: readonly string[]): number | null {
  const weight = parameters.find((parameter) => parameter.toLowerCase().startsWith(QUALITY_PREFIX));
  if (weight === undefined) {
    return DEFAULT_QUALITY;
  }
  const value = weight.slice(QUALITY_PREFIX.length);
  return QUALITY.test(value) ? Number(value) : null;
}

function rangeOf(part: string, index: number): LanguageRange | null {
  const [tag, ...parameters] = part.split(PARAMETER_SEPARATOR).map((piece) => piece.trim());
  const quality = qualityOf(parameters);
  if (!LANGUAGE_TAG.test(tag) || quality === null || quality === 0) {
    return null;
  }
  return { tag: tag.toLowerCase(), quality, index };
}

/**
 * The language tags of an `Accept-Language` header, most wanted first, lower-cased. Ranges
 * refused with `q=0`, the wildcard and malformed entries are dropped rather than failing
 * the header: a browser's preferences are only a hint.
 */
export function parseAcceptLanguage(header: string | null | undefined): string[] {
  return (header ?? '')
    .split(RANGE_SEPARATOR)
    .slice(0, MAX_RANGES)
    .map((part, index) => rangeOf(part, index))
    .filter((range): range is LanguageRange => range !== null)
    .sort((left, right) => right.quality - left.quality || left.index - right.index)
    .map((range) => range.tag);
}
