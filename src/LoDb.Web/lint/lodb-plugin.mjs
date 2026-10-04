import { featureBoundaries } from './feature-boundaries.mjs';
import { nativePluginImports } from './native-plugin-imports.mjs';

/**
 * Local ESLint plugin carrying the two blocking architecture rules of the front
 * (docs/reecriture/plan-implementation.md, section 4).
 *
 * @type {import('eslint').ESLint.Plugin}
 */
export const lodbPlugin = {
  meta: { name: 'lodb' },
  rules: {
    'feature-boundaries': featureBoundaries,
    'native-plugin-imports': nativePluginImports,
  },
};
