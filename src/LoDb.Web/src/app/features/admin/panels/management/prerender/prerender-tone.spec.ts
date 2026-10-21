import { prerenderTone } from './prerender-tone';

describe('prerenderTone', () => {
  it('reads a state it does not know as a failure', () => {
    expect(['running', 'paused', 'cancelled', 'retired', 'unknown'].map(prerenderTone)).toEqual([
      'good',
      'warn',
      'muted',
      'warn',
      'bad',
    ]);
  });

  it('reads the states of a unit', () => {
    expect(['pending', 'failed', 'stopped', 'completed'].map(prerenderTone)).toEqual([
      'muted',
      'bad',
      'warn',
      'good',
    ]);
  });
});
