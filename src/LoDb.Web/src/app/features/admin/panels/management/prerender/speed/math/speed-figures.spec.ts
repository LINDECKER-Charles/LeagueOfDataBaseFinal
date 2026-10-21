import { describe, expect, it } from 'vitest';
import { prerenderReport } from '../../../../../testing/fixtures/prerender-report';
import { prerenderSpeed } from '../../../../../testing/fixtures/prerender-speed';
import { speedFigures } from './speed-figures';

describe('speedFigures', () => {
  const report = prerenderReport();

  it('forecasts the CPU of a pace, replacing what the pass takes now', () => {
    const { cpu } = speedFigures({ pace: 600, speed: prerenderSpeed(), report });

    expect(cpu.renderers).toBeCloseTo(1);
    expect(cpu.stack).toBeCloseTo(0.3);
    // 0.4 busy + (1.3 − 0.14 − 0.042) / 4 cores.
    expect(cpu.hostBusy).toBeCloseTo(0.6795, 4);
    expect(cpu.margin).toBeCloseTo(1.282, 3);
    expect(cpu.vpsShare).toBeCloseTo(0.325);
    expect(cpu.isNearBrake).toBe(false);
  });

  it('says when a pace nears the brake', () => {
    const { cpu } = speedFigures({ pace: 900, speed: prerenderSpeed(), report });

    // 0.4 busy + (1.95 − 0.182) / 4 cores.
    expect(cpu.hostBusy).toBeCloseTo(0.842, 3);
    const speed = prerenderSpeed();
    const resources = speed.resources && { ...speed.resources, hostBusy: 0.6 };
    const hot = speedFigures({ pace: 900, speed: { ...speed, resources }, report });
    expect(hot.cpu.isNearBrake).toBe(true);
  });

  it('leaves the host unread while its CPUs or its load are unknown', () => {
    const { cpu } = speedFigures({ pace: 300, speed: prerenderSpeed({ resources: null }), report });

    expect(cpu.total).toBeCloseTo(0.65);
    expect([cpu.hostBusy, cpu.margin, cpu.vpsShare, cpu.isNearBrake]).toEqual([
      null,
      null,
      null,
      false,
    ]);
  });

  it('caps the ends at what the renderers can render', () => {
    const figures = speedFigures({ pace: 1_500, speed: prerenderSpeed(), report });

    expect(figures.capacity).toBe(1_200);
    expect(figures.isOverCapacity).toBe(true);
    expect(figures.passMinutes).toBeCloseTo(75_658 / 1_200);
  });

  it('times the active pass and the rest of the catalogue at the pace', () => {
    const figures = speedFigures({ pace: 600, speed: prerenderSpeed(), report });

    expect(figures.passPagesLeft).toBe(75_658);
    expect(figures.passMinutes).toBeCloseTo(126.1, 1);
    // 156 units missing at 91,230 pages for 8,274 complete ones.
    expect(figures.cataloguePages).toBe(1_720);
    expect(figures.catalogueMinutes).toBeCloseTo(2.87, 2);
  });

  it('prices the pages still to render at the price of the store', () => {
    const figures = speedFigures({ pace: 600, speed: prerenderSpeed(), report });

    // 1,720 pages of 101,283 bytes at 0.00803 € the GB-month.
    expect(figures.storageEuros).toBeCloseTo(0.0014, 4);
  });

  it('has no pass end without an active pass, nor a catalogue without coverage', () => {
    const idle = prerenderReport({
      current: null,
      storage: { ...report.storage, epochs: [] },
    });

    const figures = speedFigures({ pace: 600, speed: prerenderSpeed(), report: idle });

    expect([figures.passMinutes, figures.cataloguePages, figures.storageEuros]).toEqual([
      null,
      null,
      null,
    ]);
  });
});
