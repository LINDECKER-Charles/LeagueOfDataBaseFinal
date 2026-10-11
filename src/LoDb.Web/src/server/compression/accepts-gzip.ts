const GZIP_CODINGS: readonly string[] = ['gzip', 'x-gzip'];
const ANY_CODING = '*';
const WEIGHT_PREFIX = 'q=';
const FULL_WEIGHT = 1;

interface Coding {
  readonly name: string;
  readonly weight: number;
}

// An unreadable weight counts as 0: the coding is then refused rather than guessed.
function codingOf(entry: string): Coding {
  const [name = '', ...parameters] = entry.split(';').map((part) => part.trim().toLowerCase());
  const quality = parameters.find((parameter) => parameter.startsWith(WEIGHT_PREFIX));
  const weight = quality === undefined ? FULL_WEIGHT : Number(quality.slice(WEIGHT_PREFIX.length));
  return { name, weight: Number.isNaN(weight) ? 0 : weight };
}

/**
 * Whether an Accept-Encoding header admits gzip (RFC 9110, section 12.5.3): named with a
 * weight above 0, or, when it is not named at all, covered by `*` with one. Without the
 * header, the client gets the document as it is.
 */
export function acceptsGzip(acceptEncoding: string | undefined): boolean {
  const codings = (acceptEncoding ?? '').split(',').map(codingOf);
  const named = codings.find((coding) => GZIP_CODINGS.includes(coding.name));
  const chosen = named ?? codings.find((coding) => coding.name === ANY_CODING);
  return chosen !== undefined && chosen.weight > 0;
}
