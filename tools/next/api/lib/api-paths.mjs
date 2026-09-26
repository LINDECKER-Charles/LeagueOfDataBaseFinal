import { join } from 'node:path';
import { fileURLToPath } from 'node:url';

const REPO_ROOT = fileURLToPath(new URL('../../../../', import.meta.url));
const WEB_ROOT = join(REPO_ROOT, 'src', 'LoDb.Web');
const API_ROOT = join(REPO_ROOT, 'src', 'LoDb.Api');

/**
 * Locations of the API contract. Resolved from this file rather than the working
 * directory: npm runs the scripts from `src/LoDb.Web`, tests from the root.
 */
export const API_PATHS = Object.freeze({
  repoRoot: REPO_ROOT,
  webRoot: WEB_ROOT,
  apiProject: join(API_ROOT, 'LoDb.Api.csproj'),
  openApiDir: join(API_ROOT, 'openapi'),
  // Written by the SDK, named after the project and the document (LoDb.Api.csproj).
  documents: ['LoDb.Api_app.json', 'LoDb.Api_public-v1.json'],
  generatorBin: join(WEB_ROOT, 'node_modules', 'ng-openapi-gen', 'lib', 'index.js'),
  generatorConfig: join(WEB_ROOT, 'ng-openapi-gen.json'),
  clientDir: join(WEB_ROOT, 'src', 'app', 'core', 'api', 'generated'),
});
