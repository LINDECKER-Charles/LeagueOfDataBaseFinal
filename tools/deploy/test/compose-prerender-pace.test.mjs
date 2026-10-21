import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import { mkdtempSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { after, describe, it } from 'node:test';
import { fileURLToPath } from 'node:url';

// The pre-rendering pass's pace, as Compose hands it to the API and to the SSR on each stack.
// The pass renders on its own SSR (web-ssr-prerender, ADR 0011), never on the visitors':
// - the API's regulator moves the pace between Pace__MinRendersPerMinute and
//   Pace__MaxRendersPerMinute, and keeps MaxInFlight pages in flight;
// - the pass SSR's lane admits LODB_PRERENDER_RENDERS_PER_MINUTE renders a minute and
//   LODB_PRERENDER_MAX_IN_FLIGHT at once: its budget is the ceiling, so that the SSR itself
//   caps the pass even if the regulator misbehaves, and a ceiling raised on a host is raised
//   on both sides;
// - RendersPerMinute is the fixed pace (kill switch, `prerender unit`), which the lane would
//   refuse above the ceiling.
// Both sides read the same lines.
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
// A budget an operator raised in a host's .env, once measured (configuration.md, § 4.10): the
// ceiling to 300, the fixed pace and the pages in flight with it.
const RAISED = 'LODB_PRERENDER_PACE_MAX=300\nLODB_PRERENDER_RENDERS_PER_MINUTE=240\n'
  + 'LODB_PRERENDER_MAX_IN_FLIGHT=3\n';
const PASS_ALIAS = 'lodb-web-ssr-prerender';
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

/** What the API asks of the pass SSR's lane, and what the lane admits. */
function paceOf(stack, extra) {
  const services = render(stack, extra);
  const { api } = services;
  const lane = services['web-ssr-prerender'].environment;
  const env = api.environment;
  return {
    api: {
      min: env.LoDb__Prerender__Pace__MinRendersPerMinute,
      max: env.LoDb__Prerender__Pace__MaxRendersPerMinute,
      step: env.LoDb__Prerender__Pace__StepRendersPerMinute,
      fixed: env.LoDb__Prerender__RendersPerMinute,
      maxInFlight: env.LoDb__Prerender__MaxInFlight,
    },
    lane: {
      perMinute: lane.LODB_PRERENDER_RENDERS_PER_MINUTE || SSR_DEFAULTS.perMinute,
      maxInFlight: lane.LODB_PRERENDER_MAX_IN_FLIGHT || SSR_DEFAULTS.maxInFlight,
    },
    visitorsLane: {
      perMinute: services['web-ssr'].environment.LODB_PRERENDER_RENDERS_PER_MINUTE,
      maxInFlight: services['web-ssr'].environment.LODB_PRERENDER_MAX_IN_FLIGHT,
    },
  };
}

describe("the pass's pace through Compose", { skip: !hasCompose && 'no docker compose' }, () => {
  after(() => rmSync(folder, { recursive: true, force: true }));

  it("reads the SSR's own defaults", () => {
    assert.deepEqual(SSR_DEFAULTS, { perMinute: '120', maxInFlight: '2' });
  });

  for (const stack of Object.keys(STACKS)) {
    it(`gives the pass SSR's lane the API's ceiling and pages in flight on ${stack}`, () => {
      const { api, lane } = paceOf(stack);
      assert.deepEqual({ perMinute: api.max, maxInFlight: api.maxInFlight }, lane);
    });

    it(`keeps the floor, the fixed pace and the ceiling in order on ${stack}`, () => {
      const { api } = paceOf(stack);
      assert.ok(Number(api.min) <= Number(api.max), 'floor under the ceiling');
      assert.ok(Number(api.fixed) <= Number(api.max), 'the lane would refuse a fixed pace above');
      if (api.step !== undefined) {
        assert.ok(Number(api.step) <= Number(api.max), 'a step within the range');
      }
    });
  }

  it('gives staging a floor of 10, its former pace as the ceiling and one page in flight', () => {
    assert.deepEqual(paceOf('staging').api,
      { min: '10', max: '30', step: '10', fixed: '30', maxInFlight: '1' });
  });

  it('gives prod a floor of 30, a ceiling of 240 and the SSR default pages in flight', () => {
    assert.deepEqual(paceOf('prod').api,
      { min: '30', max: '240', step: undefined, fixed: '120', maxInFlight: '2' });
  });

  it('gives a local stack a wide range, to watch the regulator climb', () => {
    assert.deepEqual(paceOf('dev').api,
      { min: '120', max: '1200', step: '120', fixed: '120', maxInFlight: '2' });
  });

  for (const stack of Object.keys(STACKS)) {
    it(`sends the pass to its own SSR and the regulator to both /load on ${stack}`, () => {
      const { api, 'web-ssr': visitors, 'web-ssr-prerender': pass } = render(stack);
      const env = api.environment;
      assert.equal(env.LoDb__Prerender__SsrBaseUrl, `http://${PASS_ALIAS}:4000`);
      assert.equal(env.LoDb__Prerender__Pace__Mode, 'adaptive');
      assert.equal(env.LoDb__Prerender__Pace__VisitorsLoadUrl, 'http://lodb-web-ssr:9464/load');
      assert.equal(env.LoDb__Prerender__Pace__PassLoadUrl, `http://${PASS_ALIAS}:9464/load`);
      assert.deepEqual(pass.networks.default.aliases, [PASS_ALIAS]);
      assert.deepEqual(visitors.networks.default.aliases, ['lodb-web-ssr']);
    });
  }

  for (const stack of ['staging', 'prod']) {
    it(`follows a ceiling and a budget raised in the .env of ${stack}`, () => {
      const { api, lane, visitorsLane } = paceOf(stack, RAISED);
      const isProd = stack === 'prod';
      assert.deepEqual(api, {
        min: isProd ? '30' : '10',
        max: '300',
        step: isProd ? undefined : '10',
        fixed: '240',
        maxInFlight: '3',
      });
      assert.deepEqual(lane, { perMinute: '300', maxInFlight: '3' });
      // The visitors' lane still follows the pass lines, unused since the pass left it.
      assert.deepEqual(visitorsLane, { perMinute: '240', maxInFlight: '3' });
    });
  }

  it('lets the .env switch the regulator off, the fixed pace then applying', () => {
    const { api } = render('prod', 'LODB_PRERENDER_PACE_MODE=fixed\n');
    assert.equal(api.environment.LoDb__Prerender__Pace__Mode, 'fixed');
  });
});
