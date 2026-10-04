import { join } from 'node:path';
import { fileURLToPath } from 'node:url';

const REPO_ROOT = fileURLToPath(new URL('../../../', import.meta.url));
const WEB_ROOT = join(REPO_ROOT, 'src', 'LoDb.Web');

/**
 * Locations shared by the conversion and the report. Resolved from this file rather than
 * the working directory: npm runs the scripts from `src/LoDb.Web`, tests from the root.
 */
export const I18N_PATHS = Object.freeze({
  repoRoot: REPO_ROOT,
  webRoot: WEB_ROOT,
  yamlDir: join(REPO_ROOT, 'legacy', 'app', 'translations'),
  catalogueDir: join(WEB_ROOT, 'public', 'i18n'),
  reportFile: join(REPO_ROOT, 'docs', 'reecriture', 'rapports', 'i18n-completude.md'),
});
