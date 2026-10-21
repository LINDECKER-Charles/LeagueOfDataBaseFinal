import type { AdminProblem } from '../../../../../shared/http/problem-of';
import { launchErrorsOf } from './launch-errors-of';

const ERRORS = 'prerender.launcher.errors';

function refused(errors: AdminProblem['errors']): AdminProblem {
  return { status: 400, code: 'validation-failed', errors };
}

describe('launchErrorsOf', () => {
  it('sends the patches back to step 1', () => {
    expect(launchErrorsOf(refused({ versions: ['none-pinned'], priority: ['too-many'] }))).toEqual({
      step: 1,
      fields: {
        versions: [{ key: `${ERRORS}.versions.none-pinned` }],
        priority: [{ key: `${ERRORS}.priority.too-many` }],
      },
      band: null,
    });
  });

  it('sends the locales, the types and the epoch back to step 2', () => {
    for (const [field, code] of [
      ['locales', 'malformed'],
      ['pageTypes', 'required'],
      ['epoch', 'retired'],
    ]) {
      const errors = launchErrorsOf(refused({ [field]: [code] }));
      expect(errors.step).toBe(2);
      expect(errors.fields).toEqual({ [field]: [{ key: `${ERRORS}.${field}.${code}` }] });
    }
  });

  it('keeps the summary for a perimeter with nothing to render', () => {
    expect(launchErrorsOf(refused({ scope: ['nothing-to-render'] })).step).toBe(3);
  });

  it('goes back to the first step at fault of several', () => {
    expect(launchErrorsOf(refused({ scope: ['nothing-to-render'], epoch: ['retired'] })).step).toBe(
      2,
    );
  });

  it('shows an unknown code raw, and an unknown field with the perimeter', () => {
    expect(launchErrorsOf(refused({ versions: ['too-old'] })).fields.versions).toEqual([
      { key: `${ERRORS}.unknown`, params: { codes: 'too-old' } },
    ]);
    expect(launchErrorsOf(refused({ oldest: ['malformed'] }))).toEqual({
      step: 3,
      fields: { scope: [{ key: `${ERRORS}.unknown`, params: { codes: 'oldest: malformed' } }] },
      band: null,
    });
    expect(launchErrorsOf(refused({})).fields.scope).toEqual([
      { key: `${ERRORS}.unknown`, params: { codes: 'validation-failed' } },
    ]);
  });

  it('raises a band for a conflict, a session and no answer', () => {
    const band = (status: number, code: string | null = null) =>
      launchErrorsOf({ status, code, errors: {} }).band;

    expect(band(409, 'prerender-pass-active')).toBe('active');
    expect(band(409, 'prerender-dormant')).toBe('dormant');
    expect(band(401)).toBe('session');
    expect(band(403)).toBe('session');
    expect(band(0)).toBe('network');
    expect(band(502)).toBe('network');
  });
});
