import { HttpErrorResponse } from '@angular/common/http';

/** What a refused message tells the form: its status and the codes of its fields. */
export interface ContactProblem {
  /** HTTP status; 0 when the call got no answer. */
  readonly status: number;
  readonly errors: Readonly<Record<string, readonly string[]>>;
}

// How HttpClient reports a request that got no answer: unreachable API, aborted call.
const NO_RESPONSE = 0;

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null && !Array.isArray(value);
}

// The operation answers text (a 204), so its errors come as text too: parse them back.
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

function codesOf(value: unknown): readonly string[] {
  return Array.isArray(value)
    ? value.filter((code): code is string => typeof code === 'string')
    : [];
}

/** The problem a failed send reports; anything but an HTTP error reads as no answer. */
export function contactProblem(error: unknown): ContactProblem {
  if (!(error instanceof HttpErrorResponse)) {
    return { status: NO_RESPONSE, errors: {} };
  }
  const body = bodyOf(error);
  const errors = isRecord(body) && isRecord(body['errors']) ? body['errors'] : {};
  return {
    status: error.status,
    errors: Object.fromEntries(
      Object.entries(errors).map(([field, codes]) => [field, codesOf(codes)]),
    ),
  };
}
