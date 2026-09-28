import { createRequire } from 'node:module';
import { join } from 'node:path';
import { I18N_PATHS } from './i18n-paths.mjs';

const requireFromWeb = createRequire(join(I18N_PATHS.webRoot, 'package.json'));

/**
 * Loads a package installed by `npm ci --prefix src/LoDb.Web`. `tools/` has no
 * `node_modules` of its own, and the report must validate messages with the very
 * MessageFormat build the application ships.
 */
export function requireWebPackage(name) {
  return requireFromWeb(name);
}
