// Summary of a bench run, from the files run.sh leaves in its folder: summary.json and
// summary.md. Exits 1 when a scenario misses a criterion, 2 when one left no k6 summary.
//
//   node tools/load/summarize.mjs <run-folder>
//
// Per scenario: <S>.k6.json (bench.mjs's handleSummary), <S>.stats.ndjson (docker stats
// samples), <S>.cpu.txt (cgroup counters before and after), <S>.health.ndjson (OOM kills,
// restarts and killed processes of each container), <S>.admission.ndjson and
// <S>.api-events.txt (what the SSR and the API logged meanwhile).

import { existsSync, readFileSync, writeFileSync } from 'node:fs';
import { join } from 'node:path';
import { resultOf } from './lib/k6-result.mjs';
import { scenarioOf } from './lib/plan.mjs';
import { renderReport } from './lib/report.mjs';
import { admissionOf, cpuOf, eventsOf, samplesOf, usageOf } from './lib/stats.mjs';
import { verdictOf } from './lib/verdict.mjs';

const EXIT_MISSED = 1;
const EXIT_INCOMPLETE = 2;

function linesOf(path) {
  return existsSync(path) ? readFileSync(path, 'utf8').split('\n').filter(Boolean) : [];
}

function containersOf(folder, name, project) {
  const samples = samplesOf(linesOf(join(folder, `${name}.stats.ndjson`)), project);
  const cpu = cpuOf(linesOf(join(folder, `${name}.cpu.txt`)));
  const health = new Map(linesOf(join(folder, `${name}.health.ndjson`))
    .map((line) => JSON.parse(line))
    .map((entry) => [entry.service, entry]));
  const services = new Set([...samples.keys(), ...health.keys()]);
  return Object.fromEntries([...services].map((service) => [service, {
    ...usageOf(samples.get(service) ?? []),
    cgroup: cpu[service] ?? null,
    health: health.get(service) ?? null,
  }]));
}

function scenarioSummary(folder, name, project) {
  const k6File = join(folder, `${name}.k6.json`);
  if (!existsSync(k6File)) {
    return null;
  }
  const result = resultOf(JSON.parse(readFileSync(k6File, 'utf8')));
  const containers = containersOf(folder, name, project);
  const { rate, crowd } = scenarioOf(name);
  const logs = {
    admission: admissionOf(linesOf(join(folder, `${name}.admission.ndjson`))),
    apiEvents: eventsOf(linesOf(join(folder, `${name}.api-events.txt`))),
  };
  const verdict = verdictOf({ ...result, containers });
  return { name, rate, crowd, result, containers, logs, verdict };
}

function main(folder) {
  const run = JSON.parse(readFileSync(join(folder, 'run.json'), 'utf8'));
  const scenarios = run.scenarios.map((name) => scenarioSummary(folder, name, run.project));
  const complete = scenarios.filter((scenario) => scenario !== null);
  const summary = { run, scenarios: complete, isMet: complete.every((s) => s.verdict.isMet) };
  writeFileSync(join(folder, 'summary.json'), `${JSON.stringify(summary, null, 2)}\n`);
  writeFileSync(join(folder, 'summary.md'), renderReport(summary));
  console.log(renderReport(summary));
  if (complete.length < scenarios.length) {
    return EXIT_INCOMPLETE;
  }
  return summary.isMet ? 0 : EXIT_MISSED;
}

const [folder] = process.argv.slice(2);
if (!folder) {
  console.error('Usage: node tools/load/summarize.mjs <run-folder>');
  process.exit(EXIT_INCOMPLETE);
}
process.exit(main(folder));
