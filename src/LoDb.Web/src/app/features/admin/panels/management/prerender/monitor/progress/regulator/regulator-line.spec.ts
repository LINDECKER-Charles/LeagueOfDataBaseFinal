import type { AdminText } from '../../../../../../shared/inject-admin-text';
import { prerenderPace } from '../../../../../../testing/fixtures/prerender-progress';
import { regulatorLine } from './regulator-line';

// Texts read as `key {params}`, so a spec sees which key is chosen and what it is given.
const TEXTS: AdminText = {
  text: (key, params) => `${key} ${JSON.stringify(params ?? {})}`,
  term: (_group, name) => name,
};
const REGULATOR = 'prerender.live.pace.regulator';
const REASONS = 'prerender.live.pace.reasons';

function line(state: string, changes: Parameters<typeof prerenderPace>[1] = {}) {
  return regulatorLine(prerenderPace(state, changes), TEXTS);
}

describe('regulatorLine', () => {
  it('says a pass that starts carefully, with its pace', () => {
    expect(line('starting', { rendersPerMinute: 30, hostCpu: null })).toBe(
      `${REGULATOR}.starting {"rate":30}`,
    );
  });

  it('says a climb up to the pace, with the CPU of the VPS', () => {
    expect(line('climbing')).toBe(
      `${REGULATOR}.climbing {"rate":90} · ${REGULATOR}.cpu {"cpu":"37 %"}`,
    );
  });

  it('drops the CPU part while the regulator has no reading', () => {
    expect(line('climbing', { hostCpu: null })).toBe(`${REGULATOR}.climbing {"rate":90}`);
    expect(line('steady', { reason: 'ceiling', hostCpu: null })).toBe(
      `${REGULATOR}.ceiling {"rate":90}`,
    );
  });

  it('says the ceiling when it is the reason a steady pace holds', () => {
    expect(line('steady', { rendersPerMinute: 240, reason: 'ceiling' })).toBe(
      `${REGULATOR}.ceiling {"rate":240} · ${REGULATOR}.cpu {"cpu":"37 %"}`,
    );
  });

  it('gives the reason of any other steady pace', () => {
    expect(line('steady', { reason: 'visitors-ssr' })).toBe(
      `${REGULATOR}.steady {"rate":90,"reason":"${REASONS}.visitors-ssr {\\"cpu\\":\\"37 %\\"}"}`,
    );
  });

  it('gives the reason of a slowed pace', () => {
    expect(line('slowed', { rendersPerMinute: 45, reason: 'pass-latency' })).toContain(
      `${REGULATOR}.slowed {"rate":45,"reason":"${REASONS}.pass-latency`,
    );
  });

  it('shows the CPU in the reason of the host when it is known, else says it busy', () => {
    expect(line('slowed', { reason: 'host-cpu' })).toContain(
      `${REASONS}.host-cpu {\\"cpu\\":\\"37 %\\"}`,
    );
    expect(line('slowed', { reason: 'host-cpu', hostCpu: null })).toContain(
      `${REASONS}.host-cpu-busy`,
    );
  });

  it('says when a held pass resumes, in UTC, with the reason', () => {
    const held = line('held', {
      rendersPerMinute: 0,
      reason: 'host-steal',
      heldUntil: '2026-10-21T10:05:30+02:00',
    });

    expect(held).toContain(`${REGULATOR}.held {"at":"21/10/2026 08:05","reason":`);
    expect(held).toContain(`${REASONS}.host-steal`);
  });

  it('says a fixed pace, whatever the reason the API gives', () => {
    expect(line('fixed', { rendersPerMinute: 120, reason: 'fixed', hostCpu: null })).toBe(
      `${REGULATOR}.fixed {"rate":120}`,
    );
  });

  it('says nothing of a state or a reason it does not know', () => {
    expect(line('galloping')).toBeNull();
    expect(line('slowed', { reason: 'solar-flare' })).toBeNull();
    expect(line('held', { reason: null })).toBeNull();
    expect(line('steady', { reason: null })).toBeNull();
  });
});
