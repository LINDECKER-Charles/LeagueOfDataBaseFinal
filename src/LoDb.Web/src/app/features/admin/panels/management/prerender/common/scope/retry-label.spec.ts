import type { AdminText } from '../../../../../shared/inject-admin-text';
import type { FailuresPreset } from './failures-preset';
import { retryLabel } from './retry-label';

// The key and its values, as the label would read them.
const TEXTS: AdminText = {
  text: (key, params) => `${key} ${JSON.stringify(params)}`,
  term: (_, name) => name,
};

function preset(failed: number, gaps: number): FailuresPreset {
  return {
    preset: {
      origin: 'failures',
      versions: ['16.18.1'],
      priority: [],
      locales: null,
      pageTypes: null,
      epoch: 2,
      skipComplete: true,
      fromPass: 12,
    },
    units: failed + gaps,
    failed,
    gaps,
    pages: 4_316,
  };
}

describe('retryLabel', () => {
  it('relaunches the failures alone', () => {
    expect(retryLabel(preset(4, 0), TEXTS)).toBe(
      'prerender.common.retry.units {"units":4,"pages":4316}',
    );
  });

  it('finishes the units a cancelled pass never rendered, which are no failures', () => {
    expect(retryLabel(preset(0, 4), TEXTS)).toBe(
      'prerender.common.retry.gaps {"gaps":4,"pages":4316}',
    );
  });

  it('names both when both are left', () => {
    expect(retryLabel(preset(2, 4), TEXTS)).toBe(
      'prerender.common.retry.units_gaps {"failed":2,"gaps":4,"pages":4316}',
    );
  });
});
