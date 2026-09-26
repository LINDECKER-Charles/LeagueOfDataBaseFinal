import { HttpErrorResponse, HttpStatusCode } from '@angular/common/http';
import { updateRequirementOf } from './update-requirement-of';

function failure(status: number, error: unknown): HttpErrorResponse {
  return new HttpErrorResponse({ status, error });
}

describe('updateRequirementOf', () => {
  it('reads the versions of the ProblemDetails of a 426', () => {
    const problem = {
      code: 'client-upgrade-required',
      clientVersion: '1.3.0',
      minimumVersion: '1.4.0',
      latestVersion: '1.5.2',
    };

    expect(updateRequirementOf(failure(HttpStatusCode.UpgradeRequired, problem))).toEqual({
      clientVersion: '1.3.0',
      minimumVersion: '1.4.0',
      latestVersion: '1.5.2',
    });
  });

  it('blocks on any 426, leaving out what the body lacks or mistypes', () => {
    const odd = { minimumVersion: 14, latestVersion: null };

    expect(updateRequirementOf(failure(HttpStatusCode.UpgradeRequired, odd))).toEqual({
      clientVersion: null,
      minimumVersion: null,
      latestVersion: null,
    });
    expect(updateRequirementOf(failure(HttpStatusCode.UpgradeRequired, 'Upgrade'))).toEqual({
      clientVersion: null,
      minimumVersion: null,
      latestVersion: null,
    });
  });

  it('ignores every other failure', () => {
    expect(updateRequirementOf(failure(HttpStatusCode.Unauthorized, {}))).toBeNull();
    expect(updateRequirementOf(failure(HttpStatusCode.ServiceUnavailable, {}))).toBeNull();
    expect(updateRequirementOf(new Error('offline'))).toBeNull();
  });
});
