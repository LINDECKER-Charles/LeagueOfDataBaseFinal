// Fails when a workflow or a local action runs an action that is not pinned to a full commit
// SHA (lib/action-pinning.mjs), so that a moved tag never changes what CI executes:
//
//   node tools/ci/check-pinned-actions.mjs [--root DIR]
//
// Reads DIR/.github/workflows/*.yml|yaml and DIR/.github/actions/**/action.yml|yaml; DIR is
// the repository by default. The archived workflows of legacy/ never run and are not read.
import { readdirSync, readFileSync } from 'node:fs';
import { join, relative } from 'node:path';
import { fileURLToPath } from 'node:url';
import { parseArgs } from 'node:util';
import { pinningProblemsOf, usesReferencesOf } from './lib/action-pinning.mjs';

const REPOSITORY = fileURLToPath(new URL('../..', import.meta.url));
const WORKFLOWS = '.github/workflows';
const ACTIONS = '.github/actions';
const WORKFLOW_FILE = /\.ya?ml$/;
const ACTION_FILE = /(?:^|[\\/])action\.ya?ml$/;

function filesUnder(root, folder, pattern, isRecursive) {
  let names;
  try {
    names = readdirSync(join(root, folder), { recursive: isRecursive });
  } catch (error) {
    if (error.code === 'ENOENT') {
      return [];
    }
    throw error;
  }
  return names.filter((name) => pattern.test(name)).map((name) => join(root, folder, name));
}

function workflowFilesOf(root) {
  return [
    ...filesUnder(root, WORKFLOWS, WORKFLOW_FILE, false),
    ...filesUnder(root, ACTIONS, ACTION_FILE, true),
  ].sort();
}

try {
  const { values } = parseArgs({ options: { root: { type: 'string' } } });
  const root = values.root ?? REPOSITORY;
  const files = workflowFilesOf(root);
  if (files.length === 0) {
    throw new Error(`no workflow under ${join(root, WORKFLOWS)}`);
  }
  let references = 0;
  let problems = 0;
  for (const file of files) {
    const text = readFileSync(file, 'utf8');
    references += usesReferencesOf(text).length;
    for (const { line, value, problem } of pinningProblemsOf(text)) {
      console.error(`${relative(root, file)}:${line}: ${value}: ${problem}`);
      problems += 1;
    }
  }
  if (problems > 0) {
    throw new Error(`${problems} of ${references} references break the pinning rules`);
  }
  console.log(`check-pinned-actions: ${references} references in ${files.length} files, pinned`);
} catch (error) {
  console.error(`check-pinned-actions: ${error.message}`);
  process.exitCode = 1;
}
