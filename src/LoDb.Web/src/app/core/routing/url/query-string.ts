interface QueryParam {
  /** Decoded name, compared with the names the routing reads. */
  readonly name: string;
  /** `name=value` exactly as the URL wrote it. */
  readonly raw: string;
}

const PAIR_SEPARATOR = '&';
const VALUE_SEPARATOR = '=';
const FORM_SPACE = /\+/g;

// A malformed escape (`%E0%A4%A`) is kept as written rather than failing the whole URL.
function decodeComponent(value: string): string {
  try {
    return decodeURIComponent(value.replace(FORM_SPACE, ' '));
  } catch {
    return value;
  }
}

/**
 * Query string that the routing rewrites without disturbing it: the parameters it does not
 * own (list filters, pagination) keep their order and their encoding byte for byte, so a
 * redirect or a context switch never alters a filter it knows nothing about.
 */
export class QueryString {
  private constructor(private readonly params: readonly QueryParam[]) {}

  /** Reads a search string, with or without its leading `?`. */
  static parse(search: string): QueryString {
    const body = search.startsWith('?') ? search.slice(1) : search;
    const params = body
      .split(PAIR_SEPARATOR)
      .filter((raw) => raw !== '')
      .map((raw) => ({ name: decodeComponent(raw.split(VALUE_SEPARATOR, 1)[0]), raw }));
    return new QueryString(params);
  }

  /** Decoded value of the first parameter so named, '' for a bare name, null when absent. */
  get(name: string): string | null {
    const param = this.params.find((candidate) => candidate.name === name);
    if (param === undefined) {
      return null;
    }
    const separator = param.raw.indexOf(VALUE_SEPARATOR);
    return separator < 0 ? '' : decodeComponent(param.raw.slice(separator + 1));
  }

  /** The same query without any parameter of these names. */
  without(...names: string[]): QueryString {
    return new QueryString(this.params.filter((param) => !names.includes(param.name)));
  }

  /** The same query with `name` set once, after the parameters it keeps. */
  with(name: string, value: string): QueryString {
    const raw = `${encodeURIComponent(name)}${VALUE_SEPARATOR}${encodeURIComponent(value)}`;
    return new QueryString([...this.without(name).params, { name, raw }]);
  }

  /** `?a=1&b=2`, or '' when no parameter is left. */
  toString(): string {
    return this.params.length === 0
      ? ''
      : `?${this.params.map((param) => param.raw).join(PAIR_SEPARATOR)}`;
  }
}
