// `npm run api:check` (src/LoDb.Web), run by the `contract` job of next-ci.yml: regenerates
// the OpenAPI documents and the TypeScript client, then fails when they differ from the
// committed ones. The fix is to run `npm run api:generate` and commit what it writes.
import { findDrift } from './lib/find-drift.mjs';
import { generateContract } from './lib/generate-contract.mjs';

try {
  generateContract();
  const drift = findDrift();
  if (drift.length === 0) {
    console.log('api:check: the API contract matches the committed documents and client.');
  } else {
    console.error('api:check: the API contract drifted from the committed files:');
    drift.forEach(({ status, path }) => console.error(`  ${status.padEnd(2)} ${path}`));
    console.error('Run `npm --prefix src/LoDb.Web run api:generate` and commit the result.');
    process.exitCode = 1;
  }
} catch (error) {
  console.error(`api:check failed: ${error.message}`);
  process.exitCode = 1;
}
