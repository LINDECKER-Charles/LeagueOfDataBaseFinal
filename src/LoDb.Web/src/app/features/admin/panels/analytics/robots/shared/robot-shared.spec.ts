import type { AdminText } from '../../../../shared/inject-admin-text';
import { robotLabel, robotOperator } from './robot-label';
import { statusTone, verdictTone } from './robot-palette';
import { categoryChart, statusChart, totalOf } from './robot-chart';

// Says the key and its parameters, as a catalogue that holds every key would.
const TEXTS: AdminText = {
  text: (key, params) => (params ? `${key} ${JSON.stringify(params)}` : key),
  term: (group, name) => `${group}.${name}`,
};

describe('robotLabel', () => {
  it('keeps the name of the catalog, and writes the two the API names in English', () => {
    const googlebot = {
      key: 'googlebot',
      name: 'Googlebot',
      operator: 'Google',
      category: 'search',
    };
    const undeclared = {
      key: 'undeclared:a.net',
      name: 'x',
      operator: 'a.net',
      category: 'undeclared',
    };

    expect(robotLabel(googlebot, TEXTS)).toBe('Googlebot');
    expect(robotLabel({ ...googlebot, key: 'no-agent' }, TEXTS)).toBe('robots.no_agent');
    expect(robotLabel(undeclared, TEXTS)).toBe('robots.undeclared_name {"network":"a.net"}');
    expect(robotOperator(undeclared)).toBe('');
    expect(robotOperator(googlebot)).toBe('Google');
  });
});

describe('robot tones', () => {
  it('reads a status and a verdict as good, warning or bad', () => {
    expect([statusTone(200), statusTone(301), statusTone(404), statusTone(503)]).toEqual([
      'good',
      'good',
      'warn',
      'bad',
    ]);
    expect([verdictTone('verified'), verdictTone('mismatch'), verdictTone('other')]).toEqual([
      'good',
      'bad',
      'muted',
    ]);
  });
});

describe('robot charts', () => {
  const lines = [
    { name: 'search', values: [1, 2] },
    { name: 'ai', values: [3, 0] },
  ];

  it('draws a line per category in its colour, and sums them day by day', () => {
    const chart = categoryChart(lines, TEXTS);

    expect(chart.series.map((line) => line.label)).toEqual([
      'robots.categories.search',
      'robots.categories.ai',
    ]);
    expect(chart.series[0]?.color).toBe('var(--color-gold)');
    expect(totalOf(lines)).toEqual([4, 2]);
    expect(totalOf([])).toEqual([]);
    expect(statusChart([{ name: '5xx', values: [1] }]).legend[0]?.color).toBe('var(--color-bad)');
  });
});
