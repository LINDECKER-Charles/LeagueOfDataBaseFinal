// `npm run api:generate` (src/LoDb.Web): builds the API with the OpenAPI generation on,
// writing src/LoDb.Api/openapi/*.json, then generates the TypeScript client of the `app`
// document into src/LoDb.Web/src/app/core/api/generated (ng-openapi-gen.json).
// Both are committed, and only by the integration agents (plan, principle 6).
// Tests: `node --test "tools/next/api/**/*.test.mjs"`.
import { generateContract } from './lib/generate-contract.mjs';

try {
  generateContract();
  console.log('api:generate: OpenAPI documents and TypeScript client regenerated.');
} catch (error) {
  console.error(`api:generate failed: ${error.message}`);
  process.exitCode = 1;
}
