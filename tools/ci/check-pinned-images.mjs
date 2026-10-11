// Fails when a Dockerfile builds from an image that is not pinned to its digest
// (lib/image-pinning.mjs), so that a moved tag never changes what the CI builds and ships:
//
//   node tools/ci/check-pinned-images.mjs [--root DIR]
//
// Reads every Dockerfile under DIR/docker (`Dockerfile`, `Dockerfile.<name>`, `<name>.Dockerfile`,
// not their .dockerignore); DIR is the repository by default. The archived stack of legacy/
// never builds and is not read.
import { readdirSync, readFileSync } from 'node:fs';
import { basename, join, relative } from 'node:path';
import { fileURLToPath } from 'node:url';
import { parseArgs } from 'node:util';
import { imageReferencesOf, pinningProblemsOf } from './lib/image-pinning.mjs';

const REPOSITORY = fileURLToPath(new URL('../..', import.meta.url));
const DOCKER = 'docker';
const DOCKERFILE = /^(?:Dockerfile(?:\.[^.]+)?|[^.]+\.Dockerfile)$/;
const IGNORE_FILE = /\.dockerignore$/;

function dockerfilesOf(root) {
  let names;
  try {
    names = readdirSync(join(root, DOCKER), { recursive: true });
  } catch (error) {
    if (error.code === 'ENOENT') {
      return [];
    }
    throw error;
  }
  return names
    .filter((name) => DOCKERFILE.test(basename(name)) && !IGNORE_FILE.test(name))
    .map((name) => join(root, DOCKER, name))
    .sort();
}

try {
  const { values } = parseArgs({ options: { root: { type: 'string' } } });
  const root = values.root ?? REPOSITORY;
  const files = dockerfilesOf(root);
  if (files.length === 0) {
    throw new Error(`no Dockerfile under ${join(root, DOCKER)}`);
  }
  let references = 0;
  let problems = 0;
  for (const file of files) {
    const text = readFileSync(file, 'utf8');
    references += imageReferencesOf(text).length;
    for (const { line, value, problem } of pinningProblemsOf(text)) {
      console.error(`${relative(root, file)}:${line}: ${value}: ${problem}`);
      problems += 1;
    }
  }
  if (problems > 0) {
    throw new Error(`${problems} of ${references} image references break the pinning rules`);
  }
  console.log(`check-pinned-images: ${references} images in ${files.length} files, pinned`);
} catch (error) {
  console.error(`check-pinned-images: ${error.message}`);
  process.exitCode = 1;
}
