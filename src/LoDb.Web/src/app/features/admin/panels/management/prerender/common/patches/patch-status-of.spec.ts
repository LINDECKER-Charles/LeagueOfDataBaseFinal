import { patchStatusOf } from './patch-status-of';

describe('patchStatusOf', () => {
  it('reads a patch complete in each locale, with or without failed pages', () => {
    expect(patchStatusOf({ cells: 'ccc' })).toBe('complete');
    expect(patchStatusOf({ cells: 'cwc' })).toBe('complete_with_failures');
  });

  it('reads a patch nothing holds as absent', () => {
    expect(patchStatusOf({ cells: '---' })).toBe('absent');
    expect(patchStatusOf({ cells: '' })).toBe('absent');
  });

  it('reads a patch failed for good where no locale holds it', () => {
    expect(patchStatusOf({ cells: 'f--' })).toBe('failed');
    expect(patchStatusOf({ cells: 'fp-' })).toBe('failed');
  });

  it('reads any other mix as partial', () => {
    expect(patchStatusOf({ cells: 'cp-' })).toBe('partial');
    expect(patchStatusOf({ cells: 'cf-' })).toBe('partial');
    expect(patchStatusOf({ cells: 'p--' })).toBe('partial');
  });
});
