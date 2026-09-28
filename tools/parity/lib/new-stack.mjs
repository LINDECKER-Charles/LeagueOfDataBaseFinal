// The new side on lodb-dev: ingestion, `catalog export` and the ddragon_asset rows.
import fs from 'node:fs';
import path from 'node:path';
import { pairs } from './sample.mjs';
import { docker, stream } from './process.mjs';
import { newStackCompose, trapVersions } from './settings.mjs';

// A clean stdout for the export; settings never follow a flag such as --stored-only.
const quiet = '--Logging:LogLevel:Default=None';

const api = (args) => [...newStackCompose, 'exec', '-T', 'api', 'dotnet', 'LoDb.Api.dll', ...args];

/**
 * Ingests the sample, one run at a time: the latest versions, then the traps. Only an
 * all-languages run promotes a version, so the sample never changes the served one.
 */
export function ingest(sample, latest) {
  const languages = ['--languages', sample.languages.join(',')];
  stream('docker', api(['ingest', '--latest', String(latest), ...languages]));
  for (const version of sample.versions.filter((v) => trapVersions.includes(v))) {
    stream('docker', api(['ingest', '--version', version, ...languages]));
  }
}

/** Writes `next/export/{version}/{language}.json`, from what the stores hold only. */
export function exportProjections(sample, runDir) {
  for (const { version, language } of pairs(sample)) {
    const target = path.join(runDir, 'next/export', version, `${language}.json`);
    fs.mkdirSync(path.dirname(target), { recursive: true });
    const json = docker(api([
      'catalog', 'export', '--version', version, '--lang', language, quiet, '--stored-only',
    ]));
    fs.writeFileSync(target, json);
  }
}

/** Writes `next/assets.json`: the manifest rows of the sample versions. */
export function copyAssets(sample, runDir) {
  const versions = sample.versions.map((version) => `'${version.replaceAll("'", '')}'`);
  const query = 'select coalesce(json_agg(a order by a.version, a.type, a.key), \'[]\') from ('
    + 'select version, type, key, status, sha256, extension from ddragon_asset '
    + `where version in (${versions.join(',')})) a`;
  const json = docker([
    ...newStackCompose, 'exec', '-T', 'postgres', 'psql', '-U', 'lodb', '-d', 'lodb',
    '-At', '-c', query,
  ]);
  fs.mkdirSync(path.join(runDir, 'next'), { recursive: true });
  fs.writeFileSync(path.join(runDir, 'next/assets.json'), json);
}
