import { HttpErrorResponse } from '@angular/common/http';

/** What a refused build call says, read from its ProblemDetails. */
export interface BuildProblem {
  /** HTTP status; 0 when no answer came back at all. */
  readonly status: number;
  /** Stable kebab-case code, such as `validation-failed`; null when the answer had none. */
  readonly code: string | null;
  /** The `build.error.{code}` codes of the invalid fields, by field. */
  readonly errors: Readonly<Record<string, readonly string[]>>;
  /** The names of the items the build's mode excludes, which the refusal names. */
  readonly unavailableItems: readonly string[];
}

// How HttpClient reports a request that got no answer: unreachable API, aborted call.
const NO_RESPONSE = 0;

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null && !Array.isArray(value);
}

function stringsOf(value: unknown): string[] {
  return Array.isArray(value) ? value.filter((entry) => typeof entry === 'string') : [];
}

// An operation answering nothing (the 204 of a delete) keeps its errors as text: parse it.
function bodyOf(error: HttpErrorResponse): Record<string, unknown> {
  let body: unknown = error.error;
  if (typeof body === 'string') {
    try {
      body = JSON.parse(body) as unknown;
    } catch {
      body = null;
    }
  }
  return isRecord(body) ? body : {};
}

function errorsOf(value: unknown): Record<string, string[]> {
  if (!isRecord(value)) {
    return {};
  }
  return Object.fromEntries(
    Object.entries(value).map(([field, codes]) => [field, stringsOf(codes)]),
  );
}

/**
 * The problem a failed build call reports. The editor switches on its `code`, the contract
 * of the API, never on its title. The account pages parse theirs alike, in their feature.
 */
export function buildProblem(error: unknown): BuildProblem {
  if (!(error instanceof HttpErrorResponse)) {
    return { status: NO_RESPONSE, code: null, errors: {}, unavailableItems: [] };
  }
  const body = bodyOf(error);
  return {
    status: error.status,
    code: typeof body['code'] === 'string' ? body['code'] : null,
    errors: errorsOf(body['errors']),
    unavailableItems: stringsOf(body['unavailableItems']),
  };
}
