import { trendOf } from './trend';

describe('trendOf', () => {
  it('reads a rise, a fall and a level figure against the period before', () => {
    expect(trendOf(150, 100)).toEqual({ direction: 'up', change: 0.5 });
    expect(trendOf(75, 100)).toEqual({ direction: 'down', change: 0.25 });
    expect(trendOf(1_002, 1_000)).toEqual({ direction: 'flat', change: 0 });
  });

  it('compares with nothing when there is no period before, or it saw nothing', () => {
    expect(trendOf(10, undefined).direction).toBe('none');
    expect(trendOf(10, null).direction).toBe('none');
    expect(trendOf(10, 0).direction).toBe('none');
  });
});
