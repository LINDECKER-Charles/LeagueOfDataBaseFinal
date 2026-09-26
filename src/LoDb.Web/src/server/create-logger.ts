import pino, { type DestinationStream, type Logger } from 'pino';

interface ExceptionFields {
  readonly class: string;
  readonly code?: string | number;
}

/**
 * Exceptions go under `exception`, as an object (docs/guides/logging.md), but without their
 * message: render errors quote the requested URL, which can carry a username or a share
 * token, and the ban on personal data outranks the message. The code (`NG04002`'s 4002,
 * `ECONNREFUSED`…) still tells failures apart. The class is the error's `name`: the production
 * bundle minifies constructor names (`RuntimeError` becomes `m`).
 */
function exception(value: unknown): ExceptionFields {
  if (!(value instanceof Error)) {
    return { class: typeof value };
  }
  const code: unknown = (value as { code?: unknown }).code;
  return typeof code === 'string' || typeof code === 'number'
    ? { class: value.name, code }
    : { class: value.name };
}

/**
 * One JSON line per event on stdout, the only stream Vector collects. The level is written as
 * a word because Vector finds it by regex in the raw line; pid and hostname are left out, the
 * container stream already identifies the process. `destination` only lets tests read the lines.
 */
export function createLogger(destination?: DestinationStream): Logger {
  return pino(
    {
      base: null,
      timestamp: pino.stdTimeFunctions.isoTime,
      formatters: { level: (label) => ({ level: label }) },
      serializers: { exception },
    },
    destination,
  );
}
