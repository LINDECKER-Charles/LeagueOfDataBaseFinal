import { HttpErrorResponse } from '@angular/common/http';
import type { ApiProblem } from './api-problem';

// How HttpClient reports a request that got no answer: unreachable API, aborted call.
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
 * The problem a failed call reports. The pages switch on its `code`, the contract of the
 * API, never on its title, written for developers. Anything but an HTTP error reads as a
 * call without an answer.
 */
export function problemOf(error: unknown): ApiProblem {
  if (!(error instanceof HttpErrorResponse)) {
    return { status: NO_RESPONSE, code: null, errors: {} };
  }
  const body = bodyOf(error);
  const problem = isRecord(body) ? body : {};
  const code = typeof problem['code'] === 'string' ? problem['code'] : null;
  return { status: error.status, code, errors: errorsOf(problem['errors']) };
}
