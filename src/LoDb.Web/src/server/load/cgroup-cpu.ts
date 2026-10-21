import { readFile } from 'node:fs/promises';
import { join } from 'node:path';

/** Where cgroup v2 mounts the container's own group. */
export const CGROUP_FOLDER = '/sys/fs/cgroup';
const PRESSURE_FILE = 'cpu.pressure';
const STAT_FILE = 'cpu.stat';
const SOME_AVG10 = /^some avg10=(\d+(?:\.\d+)?)\b/m;
const THROTTLED_USEC = /^throttled_usec (\d+)$/m;
const PERCENT = 100;
const MICROSECONDS_PER_SECOND = 1e6;

/** The share of the last 10 s some task waited for CPU, 0..1, from a `cpu.pressure` text. */
export function cpuPressureOf(text: string): number | null {
  const match = SOME_AVG10.exec(text);
  return match === null ? null : Number(match[1]) / PERCENT;
}

/** The throttled CPU time in seconds, from a `cpu.stat` text. */
export function throttledSecondsOf(text: string): number | null {
  const match = THROTTLED_USEC.exec(text);
  return match === null ? null : Number(match[1]) / MICROSECONDS_PER_SECOND;
}

async function readOrNull(path: string): Promise<string | null> {
  try {
    return await readFile(path, 'utf8');
  } catch {
    return null;
  }
}

/** The container's CPU pressure and throttling; null each when unreadable (not cgroup v2). */
export async function readCgroupCpu(
  folder: string,
): Promise<{ pressure: number | null; throttledSeconds: number | null }> {
  const [pressure, stat] = await Promise.all([
    readOrNull(join(folder, PRESSURE_FILE)),
    readOrNull(join(folder, STAT_FILE)),
  ]);
  return {
    pressure: pressure === null ? null : cpuPressureOf(pressure),
    throttledSeconds: stat === null ? null : throttledSecondsOf(stat),
  };
}
