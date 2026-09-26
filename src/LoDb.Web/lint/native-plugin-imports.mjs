import { importSources } from './import-sources.mjs';

// Capacitor and Capawesome packages only exist in the Android shell: any import outside the
// platform adapters would leak native code into the web and desktop bundles.
const NATIVE_PLUGIN_SOURCE = /^@(capacitor|capawesome)\//;

/**
 * Reports any import of a native plugin. The configuration applies it to every source file
 * except `src/app/core/platform/**`, the only place allowed to talk to the device.
 *
 * @type {import('eslint').Rule.RuleModule}
 */
export const nativePluginImports = {
  meta: {
    type: 'problem',
    docs: {
      description: 'Restrict @capacitor/* and @capawesome/* imports to src/app/core/platform/.',
    },
    schema: [],
    messages: {
      forbidden:
        "'{{source}}' is a native plugin: only src/app/core/platform/ may import it, " +
        'everything else goes through PlatformService.',
    },
  },
  create(context) {
    return importSources((node, source) => {
      if (NATIVE_PLUGIN_SOURCE.test(source)) {
        context.report({ node, messageId: 'forbidden', data: { source } });
      }
    });
  },
};
