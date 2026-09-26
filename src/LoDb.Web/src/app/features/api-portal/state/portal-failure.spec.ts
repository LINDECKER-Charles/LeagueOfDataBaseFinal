import { HttpErrorResponse } from '@angular/common/http';
import { portalFailure } from './portal-failure';

function refusal(status: number, error: unknown = null): HttpErrorResponse {
  return new HttpErrorResponse({ status, error });
}

describe('portalFailure', () => {
  it.each([
    ['api-key-exists', 409, 'api.portal.flash.key_exists'],
    ['api-key-missing', 404, 'api.portal.flash.no_key'],
    ['api-key-required', 409, 'api.portal.flash.need_key'],
    ['already-subscribed', 409, 'api.portal.flash.already_subscribed'],
    ['email-not-verified', 403, 'auth.verify.gate_api'],
    ['xsrf-invalid', 403, 'api.portal.flash.csrf'],
    ['payments-unavailable', 503, 'api.portal.flash.stripe_unavailable'],
  ])('tells the code %s by its legacy message', (code, status, message) => {
    expect(portalFailure(refusal(status, { status, code }))).toBe(message);
  });

  it.each([
    [401, 'api.portal.flash.csrf'],
    [502, 'api.portal.flash.gateway'],
    [503, 'api.portal.flash.stripe_unavailable'],
    [500, 'apiPortal.error.failed'],
    [0, 'apiPortal.error.failed'],
  ])('falls back on the status %i without a known code', (status, message) => {
    expect(portalFailure(refusal(status, { code: 'unheard-of' }))).toBe(message);
    expect(portalFailure(refusal(status, 'Bad gateway'))).toBe(message);
  });

  it('reads anything but an HTTP refusal as a failure to retry', () => {
    expect(portalFailure(new Error('boom'))).toBe('apiPortal.error.failed');
  });
});
