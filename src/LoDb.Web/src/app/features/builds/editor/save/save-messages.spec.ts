import { HttpErrorResponse } from '@angular/common/http';
import { type BuildProblem, buildProblem } from './build-problem';
import { saveMessagesOf } from './save-messages-of';

function refusal(status: number, error: unknown): HttpErrorResponse {
  return new HttpErrorResponse({ status, error });
}

function problem(values: Partial<BuildProblem>): BuildProblem {
  return { status: 400, code: null, errors: {}, unavailableItems: [], ...values };
}

describe('buildProblem', () => {
  it('reads the code, the field codes and the items the mode excludes', () => {
    const error = refusal(400, {
      code: 'validation-failed',
      errors: { structure: ['steps.item_mode', 42], name: ['name.length'] },
      unavailableItems: ['Guardian Angel'],
    });

    expect(buildProblem(error)).toEqual({
      status: 400,
      code: 'validation-failed',
      errors: { structure: ['steps.item_mode'], name: ['name.length'] },
      unavailableItems: ['Guardian Angel'],
    });
  });

  it('parses a problem answered as text, as a delete answers', () => {
    const error = refusal(404, JSON.stringify({ code: 'build-not-found' }));

    expect(buildProblem(error).code).toBe('build-not-found');
  });

  it('reads anything but an HTTP error as a call without an answer', () => {
    expect(buildProblem(new Error('offline'))).toEqual({
      status: 0,
      code: null,
      errors: {},
      unavailableItems: [],
    });
  });
});

describe('saveMessagesOf', () => {
  it('shows each field code as its build.error message, once', () => {
    const refused = problem({
      code: 'validation-failed',
      errors: { name: ['name.length'], structure: ['runes.primary_style', 'name.length'] },
    });

    expect(saveMessagesOf(refused)).toEqual([
      { key: 'build.error.name.length' },
      { key: 'build.error.runes.primary_style' },
    ]);
  });

  it('names the items the mode excludes', () => {
    const refused = problem({
      code: 'validation-failed',
      errors: { structure: ['steps.item_mode'] },
      unavailableItems: ['Guardian Angel', 'Zhonya'],
    });

    expect(saveMessagesOf(refused)).toEqual([
      { key: 'build.error.steps.item_mode', params: { items: 'Guardian Angel, Zhonya' } },
    ]);
  });

  it('reads an unknown field code as an invalid structure, never as nothing', () => {
    const unknown = problem({ code: 'validation-failed', errors: { structure: ['what'] } });
    const empty = problem({ code: 'validation-failed' });

    expect(saveMessagesOf(unknown)).toEqual([{ key: 'build.error.structure.invalid' }]);
    expect(saveMessagesOf(empty)).toEqual([{ key: 'build.error.structure.invalid' }]);
  });

  it.each([
    [problem({ status: 401, code: 'authentication-required' }), 'buildsEditor.errors.signed_out'],
    [problem({ status: 403, code: 'email-not-verified' }), 'buildsEditor.errors.email_unverified'],
    [problem({ status: 403, code: 'xsrf-invalid' }), 'build.error.csrf'],
    [problem({ status: 404, code: 'build-not-found' }), 'buildsEditor.errors.not_found'],
    [problem({ status: 503 }), 'build.error.catalog_unavailable'],
    [problem({ status: 401 }), 'buildsEditor.errors.signed_out'],
    [problem({ status: 500 }), 'buildsEditor.errors.generic'],
    [problem({ status: 0 }), 'buildsEditor.errors.generic'],
  ])('shows one message for a refusal of no field', (refused, key) => {
    expect(saveMessagesOf(refused)).toEqual([{ key }]);
  });
});
