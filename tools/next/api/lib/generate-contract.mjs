import { spawnSync } from 'node:child_process';
import { existsSync, mkdtempSync, rmSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { API_PATHS } from './api-paths.mjs';

/**
 * Regenerates the whole contract: the OpenAPI documents from the API, then the TypeScript
 * client from the `app` document. Throws when a step fails, with that step's output.
 */
export function generateContract(paths = API_PATHS) {
  generateDocuments(paths);
  generateClient(paths);
}

/**
 * Builds the API with the generation property on. The previous documents are deleted
 * first: a generation that silently did not run must not leave stale files that pass.
 */
function generateDocuments(paths) {
  const documents = paths.documents.map((name) => join(paths.openApiDir, name));
  documents.forEach((file) => rmSync(file, { force: true }));
  // Program runs without a real host: storage only needs a writable root, and no
  // background task may start (plan, section 5.1).
  const storageRoot = mkdtempSync(join(tmpdir(), 'lodb-openapi-'));
  try {
    run('dotnet', buildArguments(paths), {
      cwd: paths.repoRoot,
      env: { ...process.env, LoDb__Storage__Root: storageRoot, LoDb__Workers__Enabled: 'false' },
    });
  } finally {
    rmSync(storageRoot, { recursive: true, force: true });
  }
  const missing = documents.filter((file) => !existsSync(file));
  if (missing.length > 0) {
    throw new Error(`The API build wrote no OpenAPI document at ${missing.join(', ')}.`);
  }
}

function buildArguments(paths) {
  return ['build', paths.apiProject, '-c', 'Release', '-nologo', '-p:LoDbGenerateOpenApi=true'];
}

function generateClient(paths) {
  run(process.execPath, [paths.generatorBin, '--config', paths.generatorConfig], {
    cwd: paths.webRoot,
  });
}

/** Runs a command, keeping its output quiet unless it fails. */
function run(command, args, options) {
  const result = spawnSync(command, args, { ...options, encoding: 'utf8' });
  if (result.error) {
    throw new Error(`${command} could not start: ${result.error.message}`);
  }
  if (result.status !== 0) {
    throw new Error(
      `${command} ${args.join(' ')} exited with ${result.status}.\n` +
        `${result.stdout ?? ''}${result.stderr ?? ''}`,
    );
  }
}
