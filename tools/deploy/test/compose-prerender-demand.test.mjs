import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import { mkdtempSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { after, describe, it } from 'node:test';
import { fileURLToPath } from 'node:url';

// The automatic queue of the pre-rendering, as Compose hands it to the API on each stack: on
// by default everywhere (staging and prod included), reading the visitors' SSR's refused
// pages on its metrics port, never the pass's own SSR; one line of a host's .env switches it
// off or changes its share beside a manual pass.
const ROOT = fileURLToPath(new URL('../../..', import.meta.url));
const STACKS = {
  local: { files: ['compose.yaml'] },
  dev: { files: ['compose.yaml', 'compose.override.yaml'] },
  staging: {
    template: '.env.staging.example',
    files: ['compose.yaml', 'compose.deploy.yaml', 'compose.staging.yaml'],
  },
  prod: { template: '.env.prod.example', files: ['compose.yaml', 'compose.deploy.yaml'] },
};
// Written by the deploy job on the host (configuration.md); Compose requires them.
const HOST_ADDITIONS = 'LODB_EDGE_CIDR=172.18.0.0/16\nLODB_STACK_SUBNET=10.231.2.0/24\n'
  + `LODB_EDGE_SECRET=${'5e'.repeat(32)}\n`;
const IS_CALLER_SETTING = /^(?:LODB_|LoDb__|COMPOSE_)|^(?:IMAGE_TAG|APP_REVISION)$/;

const hasCompose = spawnSync('docker', ['compose', 'version'], { encoding: 'utf8' }).status === 0;
const folder = mkdtempSync(join(tmpdir(), 'lodb-prerender-demand-'));

function demandOf(stack, extra = '') {
  const { template, files } = STACKS[stack];
  const envFile = join(folder, `${stack}${extra ? '-changed' : ''}.env`);
  const host = template ? readFileSync(join(ROOT, template), 'utf8') + HOST_ADDITIONS : '';
  writeFileSync(envFile, host + extra);
  const environment = Object.fromEntries(Object.entries(process.env)
    .filter(([key]) => !IS_CALLER_SETTING.test(key)));
  const run = spawnSync('docker', ['compose', '-p', 'lodb-demand-check', '--env-file', envFile,
    ...files.flatMap((file) => ['-f', file]), 'config', '--format', 'json'],
  { cwd: ROOT, encoding: 'utf8', env: environment });
  assert.equal(run.status, 0, run.stderr);
  const env = JSON.parse(run.stdout).services.api.environment;
  return {
    enabled: env.LoDb__Prerender__Demand__Enabled,
    share: env.LoDb__Prerender__Demand__QueueSharePercent,
    url: env.LoDb__Prerender__Demand__RefusedPagesUrl,
  };
}

const SKIP = !hasCompose && 'no docker compose';

describe('the automatic queue through Compose', { skip: SKIP }, () => {
  after(() => rmSync(folder, { recursive: true, force: true }));

  for (const stack of Object.keys(STACKS)) {
    it(`is on, at half the renders beside a pass, reading the visitors' SSR on ${stack}`, () => {
      assert.deepEqual(demandOf(stack), {
        enabled: 'true',
        share: '50',
        url: 'http://lodb-web-ssr:9464/refused',
      });
    });
  }

  it("is switched off, or its share changed, by one line of a host's .env", () => {
    const changed = demandOf('prod', 'LODB_PRERENDER_DEMAND_ENABLED=false\n'
      + 'LODB_PRERENDER_DEMAND_SHARE=25\n');

    assert.deepEqual({ enabled: changed.enabled, share: changed.share },
      { enabled: 'false', share: '25' });
  });
});
