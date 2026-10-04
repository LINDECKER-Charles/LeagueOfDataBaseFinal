import path from 'node:path';
import { importSources } from './import-sources.mjs';

const FEATURES_DIR = 'features';
// Folders every feature may depend on; anything else in src/app belongs to someone else.
const SHARED_DIRS = ['core', 'ui'];

/** Converts a native path to the forward-slash form used by module specifiers. */
function toPosix(nativePath) {
  return nativePath.split(path.sep).join('/');
}

/** Returns the top-level feature owning a path relative to src/app, or null. */
function featureOf(appRelativePath) {
  const [dir, feature] = appRelativePath.split('/');
  return dir === FEATURES_DIR && feature ? feature : null;
}

/** Tells whether `feature` may depend on the file at `appRelativeTarget`. */
function isAllowed(feature, appRelativeTarget) {
  const [dir] = appRelativeTarget.split('/');
  return SHARED_DIRS.includes(dir) || featureOf(appRelativeTarget) === feature;
}

/**
 * Keeps features independent: a file in `src/app/features/<name>/` imports only `core/`,
 * `ui/` and `features/<name>/` (sub-folders included). Package imports are left alone; only
 * relative specifiers point into the application tree, since the workspace declares no path
 * alias.
 *
 * @type {import('eslint').Rule.RuleModule}
 */
export const featureBoundaries = {
  meta: {
    type: 'problem',
    docs: { description: 'A feature imports only core/, ui/ and its own folder.' },
    schema: [
      {
        type: 'object',
        properties: { appRoot: { type: 'string' } },
        required: ['appRoot'],
        additionalProperties: false,
      },
    ],
    messages: {
      forbidden:
        "Feature '{{feature}}' cannot import '{{source}}': " +
        'only core/, ui/ and features/{{feature}}/ are allowed.',
    },
  },
  create(context) {
    const [{ appRoot }] = context.options;
    const feature = featureOf(toPosix(path.relative(appRoot, context.filename)));
    if (feature === null) {
      return {};
    }
    const directory = path.dirname(context.filename);

    return importSources((node, source) => {
      if (!source.startsWith('.')) {
        return;
      }
      const target = toPosix(path.relative(appRoot, path.resolve(directory, source)));
      if (!isAllowed(feature, target)) {
        context.report({ node, messageId: 'forbidden', data: { feature, source } });
      }
    });
  },
};
