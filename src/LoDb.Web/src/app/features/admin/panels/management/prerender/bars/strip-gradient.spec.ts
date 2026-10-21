import { stripGradient } from './strip-gradient';

// The colour stops of a gradient: each ends with its two positions, « 0% 4.762% ».
function stopsOf(gradient: string): string[] {
  return gradient
    .replace(/^linear-gradient\(to right, /, '')
    .replace(/\)$/, '')
    .split(/(?<=% [\d.]+%), /);
}

describe('stripGradient', () => {
  it('paints 21 hard stops for the 21 locales of the site', () => {
    const stops = stopsOf(stripGradient('ccccwcpf-------------'));

    expect(stops).toHaveLength(21);
    expect(stops[0]).toBe('var(--color-good) 0% 4.762%');
    expect(stops[20]).toBe('var(--color-track) 95.238% 100%');
  });

  it('paints each code with its token', () => {
    expect(stopsOf(stripGradient('cpf-'))).toEqual([
      'var(--color-good) 0% 25%',
      'var(--color-gold) 25% 50%',
      'var(--color-bad) 50% 75%',
      'var(--color-track) 75% 100%',
    ]);
  });

  it('leaves a locale outside the pass, or an unknown code, transparent', () => {
    expect(stopsOf(stripGradient('r_?'))).toEqual([
      'var(--color-hex) 0% 33.333%',
      'transparent 33.333% 66.667%',
      'transparent 66.667% 100%',
    ]);
  });

  it('paints nothing without a cell', () => {
    expect(stripGradient('')).toBe('none');
  });
});
