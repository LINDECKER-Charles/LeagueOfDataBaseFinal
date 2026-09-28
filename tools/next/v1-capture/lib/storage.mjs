// Builds the storage volume go-api reads (Data Dragon datasets and daily analytics
// aggregates) in a private temporary directory: the old stack's volume is never used.
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { seedDir } from './settings.mjs';

const millisecondsPerDay = 86_400_000;
const isoDateLength = 10;
// The go-api container runs as an unprivileged user: the bind mount must be readable.
const readableDirMode = 0o755;

/** Formats the UTC day that lies `offset` days before `today`. */
export function dayBefore(today, offset) {
  const day = new Date(today.getTime() - offset * millisecondsPerDay);
  return day.toISOString().slice(0, isoDateLength);
}

/** Serialises one seeded day the way the site's rollup writes it. */
function dailyContent(day, date) {
  if (day.raw !== undefined) {
    return day.raw;
  }
  if (day.withoutEntities) {
    return JSON.stringify({ date, views: 0 });
  }
  const views = Object.values(day.entities).reduce((sum, count) => sum + count, 0);
  return JSON.stringify({ date, views, entities: day.entities });
}

/**
 * Turns the daily template into dated files relative to `today` (UTC).
 * @returns {{ name: string, content: string }[]}
 */
export function dailyFiles(template, today) {
  return template.days.map((day) => {
    const date = dayBefore(today, day.offset);
    return { name: `${date}.json`, content: dailyContent(day, date) };
  });
}

/**
 * Creates a fresh volume directory holding `storage/` for one group.
 * @returns {string} the directory to bind-mount into go-api
 */
export function createVolume(today) {
  const volumeDir = fs.mkdtempSync(path.join(os.tmpdir(), 'lodb-v1-capture-'));
  fs.chmodSync(volumeDir, readableDirMode);
  const storageDir = path.join(volumeDir, 'storage');
  fs.cpSync(path.join(seedDir, 'storage'), storageDir, { recursive: true });
  const dailyDir = path.join(storageDir, 'analytics', 'daily');
  fs.mkdirSync(dailyDir, { recursive: true, mode: readableDirMode });
  const template = JSON.parse(fs.readFileSync(path.join(seedDir, 'daily.json'), 'utf8'));
  for (const file of dailyFiles(template, today)) {
    fs.writeFileSync(path.join(dailyDir, file.name), file.content);
  }
  return volumeDir;
}

/** Deletes a volume directory created by createVolume. */
export function removeVolume(volumeDir) {
  fs.rmSync(volumeDir, { recursive: true, force: true });
}
