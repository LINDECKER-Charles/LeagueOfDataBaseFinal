import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import { mkdtempSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { after, describe, it } from 'node:test';
import { fileURLToPath } from 'node:url';

// The pre-rendering pass's pace, as Compose hands it to the API and to the SSR on each stack:
// the API spaces its renders at LoDb__Prerender__RendersPerMinute and keeps
// LoDb__Prerender__MaxInFlight pages in flight; the SSR's prerender lane admits
// LODB_PRERENDER_RENDERS_PER_MINUTE renders a minute and LODB_PRERENDER_MAX_IN_FLIGHT at once.
// Above the lane, the pass spends its burst, then waits out a Retry-After at each refusal;
// below it, a budget raised on a host never reaches the pass. Both sides read the same lines.
const ROOT = fileURLToPath(new URL('../../..', import.meta.url));
const STACKS = {
  local: { files: ['compose.yaml'] },
  staging: {
    template: '.env.staging.example',
    files: ['compose.yaml', 'compose.deploy.yaml', 'compose.staging.yaml'],
  },
  prod: { template: '.env.prod.example', files: ['compose.yaml', 'compose.deploy.yaml'] },
};
// Written by the deploy job on the host (configuration.md); Compose requires them.
const HOST_ADDITIONS = 'LODB_EDGE_CIDR=172.18.0.0/16\nLODB_STACK_SUBNET=10.231.2.0/24\n'
  + `LODB_EDGE_SECRET=${'5e'.repeat(32)}\n`;
// A budget an operator raised in a host's .env, once measured (spec, Débit).
const RAISED = 'LODB_PRERENDER_RENDERS_PER_MINUTE=240\nLODB_PRERENDER_MAX_IN_FLIGHT=3\n';
// The SSR's own defaults, which an empty LODB_PRERENDER_* leaves in place.
const SSR_LIMITS = readFileSync(
  join(ROOT, 'src/LoDb.Web/src/server/admission/read-render-limits.ts'), 'utf8');
const SSR_DEFAULTS = {
  perMinute: SSR_LIMITS.match(/^\s+prerenderPerMinute: (\d+),$/m)?.[1],
  maxInFlight: SSR_LIMITS.match(/^\s+prerenderMaxInFlight: (\d+),$/m)?.[1],
};
// The caller's own settings outrank any env file in Compose: none of them may leak in.
const IS_CALLER_SETTING = /^(?:LODB_|LoDb__|COMPOSE_)|^(?:IMAGE_TAG|APP_REVISION)$/;

const hasCompose = spawnSync('docker', ['compose', 'version'], { encoding: 'utf8' }).status === 0;
const folder = mkdtempSync(join(tmpdir(), 'lodb-prerender-pace-'));

function render(stack, extra = '') {
  const { template, files } = STACKS[stack];
  const envFile = join(folder, `${stack}${extra ? '-raised' : ''}.env`);
  const host = template ? readFileSync(join(ROOT, template), 'utf8') + HOST_ADDITIONS : '';
  writeFileSync(envFile, host + extra);
  const environment = Object.fromEntries(Object.entries(process.env)
    .filter(([key]) => !IS_CALLER_SETTING.test(key)));
  const run = spawnSync('docker', ['compose', '-p', 'lodb-pace-check', '--env-file', envFile,
    ...files.flatMap((file) => ['-f', file]), 'config', '--format', 'json'],
  { cwd: ROOT, encoding: 'utf8', env: environment });
  assert.equal(run.status, 0, run.stderr);
  return JSON.parse(run.stdout).services;
}

/** What the API asks of the lane, and what the lane admits. */
function paceOf(stack, extra) {
  const { api, 'web-ssr': ssr } = render(stack, extra);
  return {
    api: {
      perMinute: api.environment.LoDb__Prerender__RendersPerMinute,
      maxInFlight: api.environment.LoDb__Prerender__MaxInFlight,
    },
    lane: {
      perMinute: ssr.environment.LODB_PRERENDER_RENDERS_PER_MINUTE || SSR_DEFAULTS.perMinute,
      maxInFlight: ssr.environment.LODB_PRERENDER_MAX_IN_FLIGHT || SSR_DEFAULTS.maxInFlight,
    },
  };
}

describe("the pass's pace through Compose", { skip: !hasCompose && 'no docker compose' }, () => {
  after(() => rmSync(folder, { recursive: true, force: true }));

  it("reads the SSR's own defaults", () => {
    assert.deepEqual(SSR_DEFAULTS, { perMinute: '120', maxInFlight: '2' });
  });

  for (const stack of Object.keys(STACKS)) {
    it(`gives the API the lane's own pace on ${stack}`, () => {
      const { api, lane } = paceOf(stack);
      assert.deepEqual(api, lane);
    });
  }

  it("keeps the staging's smaller lane on both sides", () => {
    assert.deepEqual(paceOf('staging').api, { perMinute: '30', maxInFlight: '1' });
  });

  for (const stack of ['staging', 'prod']) {
    it(`follows a budget raised in the .env of ${stack}`, () => {
      const { api, lane } = paceOf(stack, RAISED);
      assert.deepEqual(api, { perMinute: '240', maxInFlight: '3' });
      assert.deepEqual(lane, api);
    });
  }
});
