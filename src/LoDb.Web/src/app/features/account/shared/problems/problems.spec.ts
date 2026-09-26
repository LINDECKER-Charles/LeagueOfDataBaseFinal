import { HttpErrorResponse, HttpHeaders, HttpStatusCode } from '@angular/common/http';
import type { ApiProblem } from './api-problem';
import { fieldErrorsOf } from './field-errors-of';
import { problemOf } from './problem-of';

function refusal(status: number, error: unknown): HttpErrorResponse {
  return new HttpErrorResponse({ status, error, headers: new HttpHeaders() });
}

function validation(errors: ApiProblem['errors']): ApiProblem {
  return { status: HttpStatusCode.BadRequest, code: 'validation-failed', errors };
}

describe('problemOf', () => {
  it('reads the code and the field codes of a ProblemDetails', () => {
    const body = { code: 'validation-failed', errors: { email: ['email-taken'] } };

    expect(problemOf(refusal(HttpStatusCode.BadRequest, body))).toEqual({
      status: HttpStatusCode.BadRequest,
      code: 'validation-failed',
      errors: { email: ['email-taken'] },
    });
  });

  it('parses the text an operation answering text keeps for its errors', () => {
    const body = JSON.stringify({ code: 'invalid-token' });

    expect(problemOf(refusal(HttpStatusCode.BadRequest, body)).code).toBe('invalid-token');
  });

  it('reads no code from a body that is not a ProblemDetails', () => {
    expect(problemOf(refusal(HttpStatusCode.BadGateway, '<html>'))).toEqual({
      status: HttpStatusCode.BadGateway,
      code: null,
      errors: {},
    });
    expect(problemOf(refusal(HttpStatusCode.BadRequest, { code: 42, errors: [] })).code).toBeNull();
  });

  it('keeps only the string codes of each field', () => {
    const body = { code: 'validation-failed', errors: { username: ['required', 7], tag: 'x' } };

    expect(problemOf(refusal(HttpStatusCode.BadRequest, body)).errors).toEqual({
      username: ['required'],
      tag: [],
    });
  });

  it('reads anything but an HTTP error as a call without an answer', () => {
    expect(problemOf(new Error('offline'))).toEqual({ status: 0, code: null, errors: {} });
  });
});

describe('fieldErrorsOf', () => {
  it('says the first code of each field, translated by the account pages', () => {
    const problem = validation({
      email: ['email-taken', 'email-invalid'],
      username: ['username-invalid'],
      acceptTerms: ['terms-required'],
    });

    expect(fieldErrorsOf(problem)).toEqual({
      email: 'account.errors.email_taken',
      username: 'profile.identity.username_invalid',
      acceptTerms: 'account.errors.terms_required',
    });
  });

  it('shows a password rule as the checklist words it, the byte limit on its own', () => {
    const problem = validation({ password: ['auth.password.rule_digit'] });

    expect(fieldErrorsOf(problem)).toEqual({ password: 'auth.password.rule_digit' });
    expect(fieldErrorsOf(validation({ password: ['auth.password.too_long'] }))).toEqual({
      password: 'account.errors.password_too_long',
    });
    expect(fieldErrorsOf(validation({ password: ['auth.password.too_common'] }))).toEqual({
      password: 'auth.password.too_common',
    });
  });

  it('names an unknown code as an invalid field and skips a field without codes', () => {
    const problem = validation({ riotTagline: ['brand-new-code'], version: [] });

    expect(fieldErrorsOf(problem)).toEqual({ riotTagline: 'account.errors.invalid' });
  });
});
