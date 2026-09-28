// The legacy side: warmup, detail visits, PHP export and manifests. Console commands run
// as www-data; nothing under app/ is written, the storage volume is only mounted read-only.
import fs from 'node:fs';
import path from 'node:path';
import { labelChromas, legacyChromaLabel } from './chroma.mjs';
import { docker, run, stream } from './process.mjs';
import { pairs } from './sample.mjs';
import {
  detailChampions, legacyBaseUrl, legacyCompose, legacyMemoryLimit, legacyOutputDir,
  legacyScriptDir, legacyStorageVolume, toolDir,
} from './settings.mjs';

const php = (args) => [...legacyCompose, 'exec', '-T', '-u', 'www-data', 'php', ...args];

/** Stores datasets and first-language images of the sample, as production warms them. */
export function warm(sample) {
  stream('docker', php([
    'php', '-d', `memory_limit=${legacyMemoryLimit}`, 'bin/console', 'app:ddragon:warmup',
    '--no-ansi', `--only=${sample.versions.join(',')}`, `--langs=${sample.languages.join(',')}`,
  ]));
}

/**
 * Visits detail pages, the only path that stores a champion's detail, ability icons and
 * chromas. Returns the status and served page of every visit.
 */
export async function visitDetails(sample, champions = detailChampions) {
  const visits = [];
  for (const { version, language } of pairs(sample)) {
    for (const champion of champions) {
      // The latest version redirects to the unversioned page, a missing champion to the
      // list: the final URL says which page was served.
      const url = `${legacyBaseUrl}/${version}/champion/${champion}?lang=${language}`;
      const response = await fetch(url);
      await response.arrayBuffer();
      const served = new URL(response.url).pathname;
      visits.push({ version, language, champion, status: response.status, served });
    }
  }
  return visits;
}

/** Runs the read-only PHP export in the php container and copies its files into the run. */
export async function exportProjections(sample, runDir) {
  const target = path.join(runDir, 'legacy/export');
  fs.mkdirSync(target, { recursive: true });
  resetScript();
  try {
    for (const version of sample.versions) {
      docker(php([
        'php', '-d', `memory_limit=${legacyMemoryLimit}`, `${legacyScriptDir}/export.php`,
        `--version=${version}`, `--langs=${sample.languages.join(',')}`,
        `--out=${legacyOutputDir}`,
      ]));
    }
    docker([...legacyCompose, 'cp', `php:${legacyOutputDir}/.`, target]);
  } finally {
    removeScript();
  }
  await labelExports(target);
}

function resetScript() {
  removeScript();
  docker([...legacyCompose, 'cp', path.join(toolDir, 'php'), `php:${legacyScriptDir}`]);
  docker(php(['mkdir', '-p', legacyOutputDir]));
}

// docker compose cp writes as root: removal needs the container's root user.
function removeScript() {
  docker([...legacyCompose, 'exec', '-T', 'php', 'rm', '-rf', legacyScriptDir, legacyOutputDir]);
}

async function labelExports(target) {
  const label = await legacyChromaLabel();
  for (const file of fs.globSync('*/*.json', { cwd: target })) {
    const location = path.join(target, file);
    const projection = labelChromas(JSON.parse(fs.readFileSync(location, 'utf8')), label);
    fs.writeFileSync(location, `${JSON.stringify(projection, null, 2)}\n`);
  }
}

/**
 * Copies `manifest/{version}/` of the sample from the storage volume, mounted read-only in a
 * throwaway container; a version the legacy stack never warmed has no directory.
 */
export function copyManifests(sample, runDir) {
  const target = path.join(runDir, 'legacy');
  fs.mkdirSync(target, { recursive: true });
  const script = 'cd /storage && for v in "$@"; do [ -d "manifest/$v" ] && echo "manifest/$v"; '
    + 'done | tar -cf - -T -';
  const archive = docker([
    'run', '--rm', '-v', `${legacyStorageVolume}:/storage:ro`, 'alpine',
    'sh', '-c', script, 'manifests', ...sample.versions,
  ]);
  run('tar', ['-xf', '-', '-C', target], { input: archive });
}
