import { HttpErrorResponse } from '@angular/common/http';

/** What a failed call of the admin reports: its HTTP status and the API's `code`. */
export interface AdminProblem {
  /** 0 when the call got no answer: API unreachable, aborted or timed out. */
  readonly status: number;
  readonly code: string | null;
  /** The codes of each invalid field of a `validation-failed`. */
  readonly errors: Readonly<Record<string, readonly string[]>>;
}

// How HttpClient reports a request that got no answer.
const NO_RESPONSE = 0;

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null && !Array.isArray(value);
}

// An operation answering text (a 204) keeps the text of its errors too: parse it back.
function bodyOf(error: HttpErrorResponse): unknown {
  if (typeof error.error !== 'string') {
    return error.error;
  }
  try {
    return JSON.parse(error.error) as unknown;
  } catch {
    return null;
  }
}

function errorsOf(value: unknown): Record<string, string[]> {
  if (!isRecord(value)) {
    return {};
  }
  const fields = Object.entries(value).map(([field, codes]) => [
    field,
    Array.isArray(codes) ? codes.filter((code): code is string => typeof code === 'string') : [],
  ]);
  return Object.fromEntries(fields) as Record<string, string[]>;
}

/**
 * The problem a failed call reports. The admin switches on its `code`, the contract of the
 * API, never on its title. Anything but an HTTP error reads as a call without an answer.
 */
export function problemOf(error: unknown): AdminProblem {
  if (!(error instanceof HttpErrorResponse)) {
    return { status: NO_RESPONSE, code: null, errors: {} };
  }
  const body = bodyOf(error);
  const problem = isRecord(body) ? body : {};
  const code = typeof problem['code'] === 'string' ? problem['code'] : null;
  return { status: error.status, code, errors: errorsOf(problem['errors']) };
}
