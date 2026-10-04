import { shouldRevealImmediately } from './should-reveal-immediately';

describe('shouldRevealImmediately', () => {
  it.each([
    { reduced: true, top: 2400, height: 800, immediate: true },
    { reduced: false, top: 120, height: 800, immediate: true },
    { reduced: false, top: 800, height: 800, immediate: false },
    { reduced: false, top: 2400, height: 800, immediate: false },
  ])('reduced motion $reduced, top $top of $height: immediate $immediate', (row) => {
    expect(shouldRevealImmediately(row.reduced, row.top, row.height)).toBe(row.immediate);
  });
});
