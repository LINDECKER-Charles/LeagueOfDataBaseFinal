import { HttpErrorResponse, HttpStatusCode } from '@angular/common/http';

// The API's refusal codes and the legacy flash each one showed.
const BY_CODE: Readonly<Record<string, string>> = {
  'api-key-exists': 'api.portal.flash.key_exists',
  'api-key-missing': 'api.portal.flash.no_key',
  'api-key-required': 'api.portal.flash.need_key',
  'already-subscribed': 'api.portal.flash.already_subscribed',
  'validation-failed': 'api.portal.flash.invalid_offer',
  'payments-unavailable': 'api.portal.flash.stripe_unavailable',
  'gateway-failed': 'api.portal.flash.gateway',
  'email-not-verified': 'auth.verify.gate_api',
  'xsrf-invalid': 'api.portal.flash.csrf',
  'origin-mismatch': 'api.portal.flash.csrf',
  'authentication-required': 'api.portal.flash.csrf',
};

// A refusal without a known code, by status.
const BY_STATUS: Readonly<Partial<Record<number, string>>> = {
  [HttpStatusCode.Unauthorized]: 'api.portal.flash.csrf',
  [HttpStatusCode.ServiceUnavailable]: 'api.portal.flash.stripe_unavailable',
  [HttpStatusCode.BadGateway]: 'api.portal.flash.gateway',
};

const UNKNOWN = 'apiPortal.error.failed';

function codeOf(error: HttpErrorResponse): string | null {
  const body: unknown = error.error;
  if (typeof body !== 'object' || body === null) {
    return null;
  }
  const code = (body as { code?: unknown }).code;
  return typeof code === 'string' ? code : null;
}

/**
 * Key of the message a refused portal call shows: the ProblemDetails `code` of the API,
 * else its status; the API unreachable or anything else reads as a failure to retry.
 */
export function portalFailure(error: unknown): string {
  if (!(error instanceof HttpErrorResponse)) {
    return UNKNOWN;
  }
  const code = codeOf(error);
  return (code === null ? undefined : BY_CODE[code]) ?? BY_STATUS[error.status] ?? UNKNOWN;
}
