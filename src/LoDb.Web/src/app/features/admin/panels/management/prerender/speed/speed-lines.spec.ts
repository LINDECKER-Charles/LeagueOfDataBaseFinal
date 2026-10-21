import type { AdminText } from '../../../../shared/inject-admin-text';
import { prerenderReport } from '../../../../testing/fixtures/prerender-report';
import { prerenderSpeed } from '../../../../testing/fixtures/prerender-speed';
import { speedFigures } from './math/speed-figures';
import { speedLines } from './speed-lines';

// Texts read as `key {params}`, so a spec sees which key is chosen and what it is given.
const TEXTS: AdminText = {
  text: (key, params) => (params ? `${key} ${JSON.stringify(params)}` : key),
  term: (_group, name) => name,
};

function lines(pace: number, speed = prerenderSpeed()) {
  const figures = speedFigures({ pace, speed, report: prerenderReport() });
  return speedLines(speed, figures, TEXTS);
}

describe('speedLines', () => {
  it('reads the VPS, the renderers, the visitors and the API as last measured', () => {
    const now = lines(600).now ?? [];

    expect(now.map((row) => row.label)).toEqual([
      'prerender.speed.now.host',
      'prerender.speed.now.renderers',
      'prerender.speed.now.visitors',
      'prerender.speed.now.api',
    ]);
    expect(now[0].value).toBe(
      'prerender.speed.now.host_value {"busy":"40 %","steal":"15 %","free":"2.40","cpus":4}',
    );
    expect(now[1].value).toContain('"count":2,"cores":"0.14","pace":84');
    expect(now.some((row) => row.isWarning)).toBe(false);
  });

  it('warns of a visitor of the latest patch refused', () => {
    const speed = prerenderSpeed();
    const resources = speed.resources && { ...speed.resources, visitorsRefused: 2 };

    const now = lines(600, { ...speed, resources }).now ?? [];

    expect(now.find((row) => row.key.endsWith('visitors'))?.isWarning).toBe(true);
  });

  it('has nothing to read before the first measure', () => {
    expect(lines(600, prerenderSpeed({ resources: null })).now).toBeNull();
  });

  it('forecasts the pace, warning near the brake and past the renderers', () => {
    const calm = lines(600).forecast;
    const hard = lines(1_500).forecast;

    expect(calm.map((row) => row.key.split('.').at(-1))).toEqual([
      'cpu',
      'host',
      'capacity',
      'pass',
      'catalogue',
      'cost',
    ]);
    expect(calm.some((row) => row.isWarning)).toBe(false);
    expect(hard.filter((row) => row.isWarning).map((row) => row.key)).toEqual([
      'prerender.speed.forecast.host',
      'prerender.speed.forecast.capacity',
    ]);
    expect(calm[3].value).toBe(
      'prerender.speed.forecast.pass_value {"eta":"2 h 6 min","pages":"75 658"}',
    );
    expect(calm[5].value).toContain('"share":"32.5 %"');
  });
});
