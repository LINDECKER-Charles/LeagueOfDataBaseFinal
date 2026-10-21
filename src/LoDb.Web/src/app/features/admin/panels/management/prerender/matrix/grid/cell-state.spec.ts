import { cellStateOf } from './cell-state';

describe('cellStateOf', () => {
  it('reads each code of a pass', () => {
    expect([...'.rzcwfi_'].map((code) => cellStateOf(code, 'pass'))).toEqual([
      'pending',
      'running',
      'stopped',
      'complete',
      'complete_with_failures',
      'failed',
      'interrupted',
      'out_of_scope',
    ]);
  });

  it('reads each code of a coverage', () => {
    expect([...'cwpf-'].map((code) => cellStateOf(code, 'coverage'))).toEqual([
      'complete',
      'complete_with_failures',
      'partial',
      'failed',
      'absent',
    ]);
  });

  it('reads a code the mode does not know as out of scope', () => {
    expect(cellStateOf('p', 'pass')).toBe('out_of_scope');
    expect(cellStateOf('r', 'coverage')).toBe('out_of_scope');
    expect(cellStateOf('?', 'pass')).toBe('out_of_scope');
  });
});
