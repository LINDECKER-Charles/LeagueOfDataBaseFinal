import { HttpErrorResponse, HttpStatusCode } from '@angular/common/http';
import type { UpdateRequirement } from './update-requirement';

function textOf(body: object, field: string): string | null {
  const value = (body as Record<string, unknown>)[field];
  return typeof value === 'string' ? value : null;
}

/**
 * The requirement a failed request carries: any `426 Upgrade Required` blocks the app, the
 * versions of its ProblemDetails (`minimumVersion`, `latestVersion`, `clientVersion`) only
 * word the screen. The body is read as untrusted: a field of another type is left out.
 * Null for any other failure.
 */
export function updateRequirementOf(error: unknown): UpdateRequirement | null {
  if (!(error instanceof HttpErrorResponse) || error.status !== HttpStatusCode.UpgradeRequired) {
    return null;
  }
  const body: unknown = error.error;
  if (typeof body !== 'object' || body === null) {
    return { clientVersion: null, minimumVersion: null, latestVersion: null };
  }
  return {
    clientVersion: textOf(body, 'clientVersion'),
    minimumVersion: textOf(body, 'minimumVersion'),
    latestVersion: textOf(body, 'latestVersion'),
  };
}
