import { mkdtemp, rm, writeFile } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { cpuPressureOf, readCgroupCpu, throttledSecondsOf } from './cgroup-cpu';

const PRESSURE = [
  'some avg10=12.50 avg60=3.10 avg300=0.80 total=123456',
  'full avg10=1.00 avg60=0.20 avg300=0.05 total=6543',
].join('\n');
const STAT = [
  'usage_usec 9000000',
  'user_usec 6000000',
  'system_usec 3000000',
  'nr_periods 100',
  'nr_throttled 4',
  'throttled_usec 2500000',
].join('\n');

describe('cgroup CPU', () => {
  let folder: string;

  beforeEach(async () => {
    folder = await mkdtemp(join(tmpdir(), 'lodb-cgroup-'));
  });

  afterEach(async () => {
    await rm(folder, { recursive: true, force: true });
  });

  it('reads the some avg10 of cpu.pressure as a share', () => {
    expect(cpuPressureOf(PRESSURE)).toBeCloseTo(0.125);
    expect(cpuPressureOf('full avg10=1.00 avg60=0 avg300=0 total=1')).toBeNull();
  });

  it('reads the throttled time of cpu.stat in seconds', () => {
    expect(throttledSecondsOf(STAT)).toBe(2.5);
    expect(throttledSecondsOf('usage_usec 1')).toBeNull();
  });

  it("reads the container's files", async () => {
    await writeFile(join(folder, 'cpu.pressure'), PRESSURE);
    await writeFile(join(folder, 'cpu.stat'), STAT);

    expect(await readCgroupCpu(folder)).toEqual({ pressure: 0.125, throttledSeconds: 2.5 });
  });

  it('gives null for each file it cannot read', async () => {
    expect(await readCgroupCpu(join(folder, 'missing'))).toEqual({
      pressure: null,
      throttledSeconds: null,
    });
  });
});
