#!/usr/bin/env node
// Collects what the PHP <-> .NET parity check (L1.8) compares, into one run directory.
//
//   node tools/parity/collect.mjs                       export both sides + manifests
//   node tools/parity/collect.mjs --steps=warm,details,ingest,export,manifests
//   node tools/parity/collect.mjs --versions=16.19.1,8.7.1 --langs=en_US,fr_FR
//   node tools/parity/collect.mjs --run=tools/parity/.runs/mine
//
// The comparison itself is tests/LoDb.Parity: LODB_PARITY_RUN=<run> dotnet test ...
// warm, details and ingest write to the stores of their stack: never run two of them on the
// same stack at once.
import fs from 'node:fs';
import path from 'node:path';
import { parseArgs } from 'node:util';
import * as legacy from './lib/legacy.mjs';
import * as newStack from './lib/new-stack.mjs';
import { sampleLanguages, sampleVersions, upstreamVersions } from './lib/sample.mjs';
import { defaultLatest, runsDir } from './lib/settings.mjs';

const allSteps = ['warm', 'details', 'ingest', 'export', 'manifests'];

function options() {
  const { values } = parseArgs({
    options: {
      steps: { type: 'string', default: 'export,manifests' },
      versions: { type: 'string' },
      langs: { type: 'string' },
      latest: { type: 'string', default: String(defaultLatest) },
      run: { type: 'string' },
    },
  });
  const steps = values.steps.split(',');
  const unknown = steps.filter((step) => !allSteps.includes(step));
  if (unknown.length) {
    throw new Error(`unknown step(s): ${unknown.join(', ')}; known: ${allSteps.join(', ')}`);
  }
  const stamp = new Date().toISOString().replaceAll(/[:.]/g, '-');
  return {
    steps,
    latest: Number(values.latest),
    versions: values.versions?.split(','),
    languages: values.langs?.split(','),
    runDir: path.resolve(values.run ?? path.join(runsDir, stamp)),
  };
}

async function sampleOf(settings) {
  const upstream = settings.versions ? [] : await upstreamVersions();
  return {
    versions: sampleVersions(upstream, { latest: settings.latest, explicit: settings.versions }),
    languages: sampleLanguages(settings.languages),
    collectedAt: new Date().toISOString(),
  };
}

const stepsRun = {
  warm: (sample) => legacy.warm(sample),
  details: async (sample, settings) => {
    const visits = await legacy.visitDetails(sample);
    write(settings.runDir, 'legacy/details.json', visits);
  },
  ingest: (sample, settings) => newStack.ingest(sample, settings.latest),
  export: async (sample, settings) => {
    await legacy.exportProjections(sample, settings.runDir);
    newStack.exportProjections(sample, settings.runDir);
  },
  manifests: (sample, settings) => {
    legacy.copyManifests(sample, settings.runDir);
    newStack.copyAssets(sample, settings.runDir);
  },
};

function write(runDir, file, value) {
  const target = path.join(runDir, file);
  fs.mkdirSync(path.dirname(target), { recursive: true });
  fs.writeFileSync(target, `${JSON.stringify(value, null, 2)}\n`);
}

async function main() {
  const settings = options();
  const sample = await sampleOf(settings);
  write(settings.runDir, 'sample.json', sample);
  for (const step of allSteps.filter((known) => settings.steps.includes(known))) {
    const started = Date.now();
    await stepsRun[step](sample, settings);
    console.log(`${step}: ${Math.round((Date.now() - started) / 1000)} s`);
  }
  console.log(`run: ${settings.runDir}`);
}

await main();
