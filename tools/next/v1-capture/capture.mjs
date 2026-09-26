#!/usr/bin/env node
// Captures the observable /v1 contract of go-api into tests/fixtures/v1/references.
//
//   node tools/next/v1-capture/capture.mjs                write the references
//   node tools/next/v1-capture/capture.mjs --check        replay, compare, write nothing
//   node tools/next/v1-capture/capture.mjs --only=01-auth,06-limits
//   node tools/next/v1-capture/capture.mjs --refresh-schema [--schema-source=lodb-postgres-1]
//
// Each group runs on a freshly reset data set and a fresh go-api process, so groups are
// independent. The environment is private (own network, in-memory Postgres, temporary
// storage directory) and removed on exit, failures included.
import fs from 'node:fs';
import path from 'node:path';
import { parseArgs } from 'node:util';
import { differences } from './lib/compare.mjs';
import {
  buildImage, ensureDatabase, goApiRevision, resetData, startApi, teardown,
} from './lib/environment.mjs';
import { runGroup } from './lib/runner.mjs';
import { loadGroups } from './lib/scenarios.mjs';
import { refreshSchema } from './lib/schema.mjs';
import {
  defaultApiPort, defaultSchemaSource, referencesDir, scenariosDir, seedDir,
} from './lib/settings.mjs';
import { createVolume, removeVolume } from './lib/storage.mjs';

const maxReportedDifferences = 40;

function options() {
  const { values } = parseArgs({
    options: {
      check: { type: 'boolean', default: false },
      only: { type: 'string' },
      port: { type: 'string', default: String(process.env.V1_CAPTURE_PORT ?? defaultApiPort) },
      'refresh-schema': { type: 'boolean', default: false },
      'schema-source': { type: 'string', default: defaultSchemaSource },
    },
  });
  return { ...values, port: Number(values.port), only: values.only?.split(',') };
}

async function captureGroup(group, context) {
  const volumeDir = createVolume(new Date());
  try {
    await ensureDatabase();
    resetData();
    await startApi({ image: context.image, port: context.port, volumeDir });
    const exchanges = await runGroup(group, context);
    return { group: group.name, title: group.title, source: context.source, exchanges };
  } finally {
    removeVolume(volumeDir);
  }
}

async function captureAll(settings) {
  const keys = JSON.parse(fs.readFileSync(path.join(seedDir, 'keys.json'), 'utf8')).keys;
  const revision = goApiRevision();
  const image = buildImage(revision);
  const source = { service: 'go-api', goApiTree: revision };
  const context = { keys, image, port: settings.port, source };
  const captured = [];
  try {
    for (const group of loadGroups(scenariosDir, settings.only)) {
      console.log(`capturing ${group.name}`);
      captured.push(await captureGroup(group, context));
    }
  } finally {
    teardown();
  }
  return captured;
}

const referencePath = (name) => path.join(referencesDir, `${name}.json`);

function write(captured) {
  fs.mkdirSync(referencesDir, { recursive: true });
  for (const reference of captured) {
    fs.writeFileSync(referencePath(reference.group), `${JSON.stringify(reference, null, 2)}\n`);
    console.log(`wrote ${path.relative(process.cwd(), referencePath(reference.group))}`);
  }
}

function check(captured) {
  const found = captured.flatMap((reference) => {
    const file = referencePath(reference.group);
    if (!fs.existsSync(file)) {
      return [`${reference.group}: no committed reference`];
    }
    const expected = JSON.parse(fs.readFileSync(file, 'utf8'));
    return differences(expected, reference).map((line) => `${reference.group}${line}`);
  });
  found.slice(0, maxReportedDifferences).forEach((line) => console.error(line));
  console.log(found.length === 0
    ? `check passed: ${captured.length} group(s) identical to the references`
    : `check failed: ${found.length} difference(s)`);
  return found.length === 0;
}

async function main() {
  const settings = options();
  if (settings['refresh-schema']) {
    console.log(`schema snapshot written to ${refreshSchema(settings['schema-source'])}`);
    return;
  }
  const captured = await captureAll(settings);
  if (!settings.check) {
    write(captured);
  } else if (!check(captured)) {
    process.exitCode = 1;
  }
}

await main();
