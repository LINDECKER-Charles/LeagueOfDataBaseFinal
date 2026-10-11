import { build } from 'esbuild';

/**
 * Imports a TypeScript module of the application into a node test: bundled by the esbuild the
 * application builder runs on, then loaded from memory. For pure modules only (no Angular).
 */
export async function importTypeScript(file) {
  const { outputFiles } = await build({
    entryPoints: [file],
    bundle: true,
    format: 'esm',
    platform: 'neutral',
    write: false,
    logLevel: 'error',
  });
  const source = Buffer.from(outputFiles[0].text).toString('base64');
  return import(`data:text/javascript;base64,${source}`);
}
