// tools/prerender/scaleway-bucket.sh against the real AWS CLI of the image it pins and an S3
// played by moto, in its own pinned image: Scaleway cannot be replayed in CI, its S3 API can.
// moto checks no signature and ignores Scaleway's policy format: what the policy does on
// Scaleway is proven by `verifier` on the VPS (README.md), not here.
//
// The `docker` the script sees is docker-relay.sh: it records each call, then runs the
// container on a throwaway network where Scaleway's address names moto. The container and the
// network are removed even on failure. Needs a Docker daemon; skipped without one.
import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import { randomUUID } from 'node:crypto';
import { chmodSync, mkdtempSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { after, before, describe, test } from 'node:test';
import { fileURLToPath } from 'node:url';

const HERE = fileURLToPath(new URL('.', import.meta.url));
const SCRIPT = join(HERE, '..', 'scaleway-bucket.sh');
const RELAY = join(HERE, 'docker-relay.sh');
// The dev stack's simulated store, read from compose.override.yaml: Dependabot bumps one pin.
const MOTO = readFileSync(join(HERE, '..', '..', '..', 'compose.override.yaml'), 'utf8')
  .match(/^\s+image: (motoserver\/moto:\S+)$/m)[1];
const SCRIPT_TEXT = readFileSync(SCRIPT, 'utf8');
const AWS_CLI = SCRIPT_TEXT.match(/^readonly IMAGE_AWS_CLI="([^"@]+)@\$AWS_CLI_DIGEST"$/m)[1]
  + '@' + SCRIPT_TEXT.match(/^readonly AWS_CLI_DIGEST=(\S+)$/m)[1];
const TIMEOUT_MS = 300_000;
const MOTO_ATTEMPTS = 60;
const VPS = '187.77.173.149';
const INPUTS = ['BUCKET', 'ENVIRONMENT', 'REGION', 'PROJECT_ID', 'OPERATOR_USER_ID',
  'WRITER_APPLICATION_ID', 'VPS_IPV4', 'SERVED_EPOCH', 'WRITE_EPOCH', 'RETIRED_EPOCHS'];
// What lets moto answer an anonymous read: AWS's format, and an action of its own for HEAD.
const PUBLIC_READ = (bucket) => JSON.stringify({
  Version: '2012-10-17',
  Statement: [{ Effect: 'Allow', Principal: '*', Action: ['s3:GetObject', 's3:HeadObject'],
    Resource: [`arn:aws:s3:::${bucket}/pages/*`] }],
});
// moto checks no key: Scaleway's refusal of a listing to the writer, played by the bench.
const DENY_LISTING = `s3api() {
  if [[ "$1" == list-objects-v2 ]]; then
    echo 'An error occurred (AccessDenied) when calling the ListObjectsV2 operation' >&2
    return 254
  fi
  s3api_with_volume "" "$@"
}
verify`;
const MULTIPART = { ID: 'abandon-multipart', Status: 'Enabled', Filter: { Prefix: '' },
  AbortIncompleteMultipartUpload: { DaysAfterInitiation: 1 } };
const expiry = (id, prefix, days) =>
  ({ ID: id, Status: 'Enabled', Filter: { Prefix: prefix }, Expiration: { Days: days } });
const retire = (epoch) => expiry(`retire-pages-${epoch}`, `pages/${epoch}/`, 1);

const hasDocker = spawnSync('docker', ['info'], { encoding: 'utf8' }).status === 0;
const network = `lodb-prerender-bench-${randomUUID().slice(0, 8)}`;
const folder = mkdtempSync(join(tmpdir(), 'lodb-prerender-bench-'));
const project = randomUUID();
const operator = randomUUID();
const writer = randomUUID();

function docker(...args) {
  const run = spawnSync('docker', args, { encoding: 'utf8', timeout: TIMEOUT_MS });
  assert.equal(run.status, 0, `docker ${args.slice(0, 3).join(' ')}: ${run.stderr}`);
  return run.stdout;
}

/** The AWS CLI straight against moto, outside the script: the bucket's real state. */
function aws(...args) {
  return spawnSync('docker', ['run', '--rm', '--network', network,
    '-e', 'AWS_ACCESS_KEY_ID=bench', '-e', 'AWS_SECRET_ACCESS_KEY=bench',
    '-e', 'AWS_DEFAULT_REGION=fr-par', AWS_CLI, 's3api', '--endpoint-url', 'http://moto:5000',
    ...args], { encoding: 'utf8', timeout: TIMEOUT_MS });
}

function awsJson(...args) {
  const run = aws(...args);
  assert.equal(run.status, 0, run.stderr);
  return run.stdout.trim() ? JSON.parse(run.stdout) : {};
}

function createBucket(bucket) {
  awsJson('create-bucket', '--bucket', bucket,
    '--create-bucket-configuration', 'LocationConstraint=fr-par');
}

const rulesOf = (bucket) => awsJson('get-bucket-lifecycle-configuration', '--bucket', bucket)
  .Rules.sort((left, right) => left.ID.localeCompare(right.ID));
const policyOf = (bucket) => JSON.parse(awsJson('get-bucket-policy', '--bucket', bucket).Policy);
const writes = (calls) => calls.filter((call) => / s3api .* (put|create|delete)-/.test(call));

/** bash ARGS with the relay first in PATH; returns its result and the docker calls. */
function runBash(args, inputs) {
  const trace = join(folder, `trace-${randomUUID().slice(0, 8)}`);
  writeFileSync(trace, '');
  writeFileSync(`${trace}.keys`, '');
  const accessKey = `SCWBENCH${randomUUID().slice(0, 12).toUpperCase()}`;
  const secret = `bench-secret-${randomUUID()}`;
  const environment = Object.fromEntries(Object.entries(process.env)
    .filter(([key]) => !INPUTS.includes(key)));
  const run = spawnSync('bash', args, {
    encoding: 'utf8', timeout: TIMEOUT_MS,
    env: { ...environment, PATH: `${join(folder, 'relay')}:${process.env.PATH}`,
      TRACE: trace, NETWORK: network, REAL_DOCKER: realDocker,
      AWS_ACCESS_KEY_ID: accessKey, AWS_SECRET_ACCESS_KEY: secret, ...inputs },
  });
  const calls = readFileSync(trace, 'utf8').split('\n').filter(Boolean);
  const keys = new Set(readFileSync(`${trace}.keys`, 'utf8').split('\n').filter(Boolean));
  // The key reaches the containers through their environment (`-e NAME` without a value):
  // never on a command line, never on screen.
  for (const value of [secret, accessKey]) {
    assert.ok(!`${run.stdout}${run.stderr}${calls.join('\n')}`.includes(value), 'key leaked');
  }
  for (const call of calls.filter((line) => line.includes(' s3api '))) {
    assert.match(call, / -e AWS_ACCESS_KEY_ID -e AWS_SECRET_ACCESS_KEY /);
  }
  return { ...run, output: run.stdout + run.stderr, calls, keys, accessKey };
}

const runScript = (command, inputs) => runBash([SCRIPT, ...(command ? [command] : [])], inputs);
/** STEP after the script is sourced: one of its functions alone, with any of them replaced. */
const runSourced = (step, inputs) =>
  runBash(['-c', `source "$1"\n${step}`, 'bench', SCRIPT], inputs);

const configuration = (bucket, environment, extra = {}) => ({
  BUCKET: bucket, ENVIRONMENT: environment, PROJECT_ID: project, OPERATOR_USER_ID: operator,
  WRITER_APPLICATION_ID: writer, VPS_IPV4: VPS, ...extra,
});

let realDocker;

describe('scaleway-bucket.sh against moto', { skip: !hasDocker && 'no Docker daemon' }, () => {
  before(() => {
    realDocker = spawnSync('bash', ['-c', 'command -v docker'], { encoding: 'utf8' }).stdout.trim();
    for (const image of [AWS_CLI, MOTO]) {
      if (spawnSync('docker', ['image', 'inspect', image]).status !== 0) {
        docker('pull', '-q', image);
      }
    }
    docker('network', 'create', '--internal', network);
    docker('run', '-d', '--name', `${network}-moto`, '--network', network,
      '--network-alias', 'moto', MOTO);
    const relay = join(folder, 'relay', 'docker');
    spawnSync('mkdir', ['-p', join(folder, 'relay')]);
    writeFileSync(relay, readFileSync(RELAY));
    chmodSync(relay, 0o755);
    for (let attempt = 0; aws('list-buckets').status !== 0; attempt += 1) {
      assert.ok(attempt < MOTO_ATTEMPTS, 'moto does not answer');
      spawnSync('sleep', ['1']);
    }
  });

  after(() => {
    spawnSync('docker', ['container', 'rm', '-f', `${network}-moto`]);
    spawnSync('docker', ['network', 'rm', network]);
    rmSync(folder, { recursive: true, force: true });
  });

  test('sets up a new staging bucket, then a replay changes nothing', () => {
    const bucket = 'lodb-pages-bench-staging';
    let run = runScript(undefined, configuration(bucket, 'staging'));
    assert.equal(run.status, 0, run.output);
    assert.match(run.output, /cree, prive, sans versionnage/);
    assert.match(run.output, /cycle de vie : expire-pages abandon-multipart \(remplace : aucune/);
    assert.match(run.output, /\(remplacee : aucune\)/);
    assert.match(run.output, /LoDb__Prerender__Bucket=lodb-pages-bench-staging/);
    assert.ok(run.calls.some((call) => call.includes(
      `create-bucket --bucket ${bucket} --create-bucket-configuration LocationConstraint=fr-par`)));
    assert.deepEqual([...run.keys], [`${run.accessKey}@${project}`], 'scoped to the project');
    assert.deepEqual(rulesOf(bucket), [MULTIPART, expiry('expire-pages', 'pages/', 14)]);
    const policy = policyOf(bucket);
    assert.equal(policy.Version, '2023-04-17');
    assert.deepEqual(policy.Statement.map((statement) => [statement.Principal, statement.Action]), [
      [{ SCW: `user_id:${operator}` }, ['s3:*']],
      [{ SCW: `application_id:${writer}` }, ['s3:PutObject']],
      ['*', ['s3:GetObject']],
    ]);
    assert.deepEqual(policy.Statement[0].Resource, [bucket, `${bucket}/*`]);
    assert.deepEqual(policy.Statement[2].Resource, [`${bucket}/pages/*`, `${bucket}/build/*`]);
    assert.deepEqual(policy.Statement[2].Condition,
      { IpAddress: { 'aws:SourceIp': [`${VPS}/32`] } });

    run = runScript('configurer', configuration(bucket, 'staging'));
    assert.equal(run.status, 0, run.output);
    assert.match(run.output, /existe deja/);
    assert.equal(run.output.match(/\(deja en place\)/g)?.length, 2, run.output);
    assert.deepEqual(writes(run.calls), []);
  });

  test('corrects a drifted prod bucket and keeps the retirements under way', () => {
    const bucket = 'lodb-pages-bench-drift-prod';
    createBucket(bucket);
    awsJson('put-bucket-lifecycle-configuration', '--bucket', bucket, '--lifecycle-configuration',
      JSON.stringify({ Rules: [expiry('vider', '', 1), retire(2)] }));
    awsJson('put-bucket-policy', '--bucket', bucket, '--policy', PUBLIC_READ(bucket));
    const run = runScript(undefined, configuration(bucket, 'prod',
      { SERVED_EPOCH: '3', WRITE_EPOCH: '4', RETIRED_EPOCHS: '1' }));
    assert.equal(run.status, 0, run.output);
    assert.match(run.output, /existe deja/);
    assert.match(run.output,
      /retire-pages-0 retire-pages-1 retire-pages-2 abandon-multipart \(remplace : /);
    assert.match(run.output, /\(remplace : [^)]*vider/);
    assert.match(run.output, /\(remplacee : une autre politique\)/);
    assert.deepEqual(rulesOf(bucket), [MULTIPART, retire(0), retire(1), retire(2)]);
    assert.equal(policyOf(bucket).Statement[0].Principal.SCW, `user_id:${operator}`);
  });

  test('refuses to retire a live epoch, listed or already on the bucket', () => {
    let run = runScript(undefined, configuration('lodb-pages-bench-live-prod', 'prod',
      { SERVED_EPOCH: '5', WRITE_EPOCH: '6', RETIRED_EPOCHS: '4,5' }));
    assert.notEqual(run.status, 0);
    assert.match(run.stderr, /^Erreur : l'epoch 5 est celle que nginx sert/);
    assert.deepEqual(run.calls, []);

    const bucket = 'lodb-pages-bench-back-prod';
    createBucket(bucket);
    awsJson('put-bucket-lifecycle-configuration', '--bucket', bucket, '--lifecycle-configuration',
      JSON.stringify({ Rules: [retire(5)] }));
    run = runScript(undefined,
      configuration(bucket, 'prod', { SERVED_EPOCH: '6', WRITE_EPOCH: '5' }));
    assert.notEqual(run.status, 0);
    assert.match(run.stderr, /l'epoch 5 est celle que la passe ecrit/);
    assert.deepEqual(writes(run.calls), []);

    run = runScript(undefined,
      configuration(bucket, 'prod', { SERVED_EPOCH: '3', WRITE_EPOCH: '4' }));
    assert.notEqual(run.status, 0);
    assert.match(run.stderr, /l'epoch 5 n'a jamais ete ecrite/);
    assert.deepEqual(writes(run.calls), []);
    assert.deepEqual(rulesOf(bucket), [retire(5)]);
  });

  test('leaves a bucket that holds retirements alone when told it is the staging\'s', () => {
    const bucket = 'lodb-pages-bench-mixup-staging';
    createBucket(bucket);
    const rules = [MULTIPART, retire(0), retire(2)];
    awsJson('put-bucket-lifecycle-configuration', '--bucket', bucket, '--lifecycle-configuration',
      JSON.stringify({ Rules: rules }));
    const run = runScript(undefined, configuration(bucket, 'staging'));
    assert.notEqual(run.status, 0);
    assert.match(run.stderr, /porte des regles retire-pages-<N> : c'est celui d'un bucket de prod/);
    assert.deepEqual(writes(run.calls), []);
    assert.deepEqual(rulesOf(bucket), rules);
  });

  test('never sends a policy that leaves out the operator', () => {
    const bucket = 'lodb-pages-bench-locked-staging';
    createBucket(bucket);
    // The policy the script would build, its operator statement dropped.
    const forged = JSON.stringify({ Version: '2023-04-17', Statement: [{ Sid: 'Ecrivain',
      Effect: 'Allow', Principal: { SCW: `application_id:${writer}` },
      Action: ['s3:PutObject'], Resource: [`${bucket}/pages/*`] }] });
    const run = runSourced(`bucket_policy() { printf '%s' "$FORGED"; }
check_configuration_inputs; scope_operator_key; ensure_policy`,
    { ...configuration(bucket, 'staging'), FORGED: forged });
    assert.notEqual(run.status, 0, run.output);
    assert.match(run.stderr, /^Erreur : politique refusee : elle n'accorde pas s3:\* a l'oper/);
    assert.ok(!run.calls.some((call) => call.includes('put-bucket-policy')), run.calls.join('\n'));
    assert.match(aws('get-bucket-policy', '--bucket', bucket).stderr, /NoSuchBucketPolicy/);
  });

  test('takes a policy spelt another way for the same, without sending it again', () => {
    const bucket = 'lodb-pages-bench-spelling-staging';
    createBucket(bucket);
    const objects = [`${bucket}/pages/*`, `${bucket}/build/*`];
    // No Id nor Sid, one-element lists as strings, the anonymous principal as AWS spells it.
    awsJson('put-bucket-policy', '--bucket', bucket, '--policy', JSON.stringify({
      Version: '2023-04-17', Statement: [
        { Effect: 'Allow', Principal: { AWS: '*' }, Action: 's3:GetObject', Resource: objects,
          Condition: { IpAddress: { 'aws:SourceIp': `${VPS}/32` } } },
        { Effect: 'Allow', Principal: { SCW: [`user_id:${operator}`] }, Action: 's3:*',
          Resource: [`${bucket}/*`, bucket] },
        { Effect: 'Allow', Principal: { SCW: `application_id:${writer}` },
          Action: ['s3:PutObject'], Resource: [...objects].reverse() },
      ] }));
    const run = runScript(undefined, configuration(bucket, 'staging'));
    assert.equal(run.status, 0, run.output);
    assert.match(run.output, /politique : .* \(deja en place\)/);
    assert.ok(!run.calls.some((call) => call.includes('put-bucket-policy')), run.calls.join('\n'));
  });

  test('tells the operator whether they still get in before a policy read back otherwise', () => {
    const bucket = 'lodb-pages-bench-reread-staging';
    createBucket(bucket);
    const run = runSourced(`read_policy() { printf '{}'; }
check_configuration_inputs; scope_operator_key; ensure_policy`, configuration(bucket, 'staging'));
    assert.notEqual(run.status, 0, run.output);
    assert.match(run.stdout, /acces de l'operateur sous la politique : verifie/);
    assert.match(run.stderr, /^Erreur : politique posee, mais relue autrement\. /m);
    assert.match(run.stderr, /Posee : \{"Version":"2023-04-17","Statement":\[.*; relue : null$/m);
  });

  test('refuses a versioned bucket as it is', () => {
    const bucket = 'lodb-pages-bench-versioned-staging';
    createBucket(bucket);
    aws('put-bucket-versioning', '--bucket', bucket,
      '--versioning-configuration', 'Status=Enabled');
    const run = runScript(undefined, configuration(bucket, 'staging'));
    assert.notEqual(run.status, 0);
    assert.match(run.stderr, /le versionnage est actif/);
    assert.deepEqual(writes(run.calls), []);
  });

  test('refuses invalid inputs before any call', () => {
    const valid = configuration('lodb-pages-bench-input-prod', 'prod',
      { SERVED_EPOCH: '1', WRITE_EPOCH: '1' });
    const cases = [
      [{ BUCKET: 'avec.un.point' }, /^Erreur : BUCKET/],
      [{ BUCKET: 'Majuscules' }, /^Erreur : BUCKET/],
      [{ ENVIRONMENT: 'preprod' }, /^Erreur : ENVIRONMENT/],
      [{ OPERATOR_USER_ID: '' }, /^Erreur : OPERATOR_USER_ID .* la politique l'exclurait/],
      [{ OPERATOR_USER_ID: 'moi' }, /^Erreur : OPERATOR_USER_ID/],
      [{ WRITER_APPLICATION_ID: project.toUpperCase() }, /^Erreur : WRITER_APPLICATION_ID/],
      [{ PROJECT_ID: '' }, /^Erreur : PROJECT_ID/],
      [{ VPS_IPV4: '256.1.1.1' }, /^Erreur : VPS_IPV4/],
      [{ VPS_IPV4: '2a02:4780::1' }, /^Erreur : VPS_IPV4/],
      [{ SERVED_EPOCH: '0' }, /^Erreur : SERVED_EPOCH .* reservee aux sondes/],
      [{ RETIRED_EPOCHS: '2;3' }, /^Erreur : RETIRED_EPOCHS/],
      [{ RETIRED_EPOCHS: '3' }, /^Erreur : l'epoch 3 n'a jamais ete ecrite/],
      [{ ENVIRONMENT: 'staging' }, /^Erreur : SERVED_EPOCH : sans objet sur staging/],
      [{ ENVIRONMENT: 'staging', SERVED_EPOCH: '', WRITE_EPOCH: '' },
        /^Erreur : BUCKET : lodb-pages-bench-input-prod ne finit pas par -staging/],
      [{ BUCKET: 'lodb-pages-staging' }, /^Erreur : BUCKET : .* ne finit pas par -prod/],
      [{ AWS_SECRET_ACCESS_KEY: '' }, /^Erreur : AWS_ACCESS_KEY_ID et AWS_SECRET_ACCESS_KEY/],
      [{ AWS_ACCESS_KEY_ID: `SCWBENCH@${randomUUID()}` }, /^Erreur : AWS_ACCESS_KEY_ID nomme/],
    ];
    for (const [change, message] of cases) {
      const run = runScript(undefined, { ...valid, ...change });
      assert.notEqual(run.status, 0, JSON.stringify(change));
      assert.match(run.stderr, message, JSON.stringify(change));
      assert.deepEqual(run.calls, [], JSON.stringify(change));
    }
    const unknown = runScript('effacer', valid);
    assert.match(unknown.stderr, /^Erreur : commande inconnue/);
    assert.deepEqual(unknown.calls, []);
  });

  test('verifier writes the probe with the key as is and reads it back anonymously', () => {
    const bucket = 'lodb-pages-bench-probe';
    createBucket(bucket);
    awsJson('put-bucket-policy', '--bucket', bucket, '--policy', PUBLIC_READ(bucket));
    const run = runSourced(DENY_LISTING, { BUCKET: bucket });
    assert.equal(run.status, 0, run.output);
    assert.match(run.output, /cle d'ecriture seule : verifie \(liste du bucket refusee\)/);
    assert.match(run.output, /HEAD anonyme depuis le VPS \(IPv4\) : 200, en-tetes et metadonnees/);
    assert.match(run.output, /GET anonyme depuis le VPS \(IPv4\) : 200/);
    assert.match(run.output, /le gzip ecrit, octet pour octet/);
    // The received headers are shown for the operator to send back, request ids left out.
    assert.match(run.output,
      /en-tetes recus au GET :\n(?: {4}.+\n)*? {4}x-amz-meta-lodb-build: sonde/i);
    assert.match(run.output, /en-tetes recus au HEAD :/);
    assert.doesNotMatch(run.output, /^ {4}(date|x-amz-request-id|x-amzn-requestid):/im);
    assert.deepEqual([...run.keys], [run.accessKey], 'the writer key keeps its own project');
    assert.ok(run.calls.filter((call) => call.includes('--entrypoint curl'))
      .every((call) => call.includes(' -4 ')));
    const probe = awsJson('head-object', '--bucket', bucket, '--key', 'pages/0/_sonde');
    assert.equal(probe.StorageClass, 'ONEZONE_IA');
    assert.equal(probe.ContentEncoding, 'gzip');
    assert.equal(probe.CacheControl, 'public, max-age=3600, s-maxage=31536000');
    assert.equal(probe.Metadata['reporting-endpoints'], 'csp="/api/csp-reports"');
    assert.deepEqual(Object.keys(probe.Metadata).sort(), ['content-security-policy',
      'content-security-policy-report-only', 'link', 'lodb-build', 'reporting-endpoints']);
  });

  test('verifier stops on a probe nobody may read anonymously', () => {
    const bucket = 'lodb-pages-bench-closed';
    createBucket(bucket);
    const run = runSourced(DENY_LISTING, { BUCKET: bucket });
    assert.notEqual(run.status, 0);
    assert.match(run.stderr, /^Erreur : GET anonyme depuis le VPS : 403 au lieu de 200/);
  });

  test('verifier refuses a key that lists the bucket: not the writer\'s', () => {
    const bucket = 'lodb-pages-bench-reader';
    createBucket(bucket);
    awsJson('put-bucket-policy', '--bucket', bucket, '--policy', PUBLIC_READ(bucket));
    const run = runScript('verifier', { BUCKET: bucket });
    assert.notEqual(run.status, 0);
    assert.match(run.stderr, /^Erreur : cette cle liste le bucket : ce n'est pas celle de lodb/);
    assert.ok(!run.calls.some((call) => call.includes('--entrypoint curl')), 'no read after it');
  });

  test('supprimer-politique takes the policy off', () => {
    const bucket = 'lodb-pages-bench-recovery';
    createBucket(bucket);
    awsJson('put-bucket-policy', '--bucket', bucket, '--policy', PUBLIC_READ(bucket));
    const run = runScript('supprimer-politique', { BUCKET: bucket, PROJECT_ID: project });
    assert.equal(run.status, 0, run.output);
    assert.match(run.output, /politique supprimee/);
    assert.match(aws('get-bucket-policy', '--bucket', bucket).stderr, /NoSuchBucketPolicy/);
  });

  test('supprimer-politique stops on a failed read back with its own message', () => {
    const bucket = 'lodb-pages-bench-unread';
    createBucket(bucket);
    const run = runSourced(`read_policy() { fail 'lecture de la politique : refus (403)'; }
delete_policy`, { BUCKET: bucket, PROJECT_ID: project });
    assert.notEqual(run.status, 0);
    assert.match(run.stderr, /^Erreur : lecture de la politique : refus \(403\)$/m);
    assert.doesNotMatch(run.output, /encore la|politique supprimee/);
  });
});
