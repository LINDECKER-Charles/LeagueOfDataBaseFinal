// Loading, validation and expansion of the scenario groups (tests/fixtures/v1/scenarios).
import fs from 'node:fs';
import path from 'node:path';

export const actions = Object.freeze(['sleep', 'sql', 'stopDatabase', 'hideStorage']);
const keyPlaceholder = /\{\{key:([a-z_]+)\}\}/g;
const repeatSeparator = '#';

/** Throws when a step is neither a request nor a known action, or lacks its fields. */
export function validateStep(step, group) {
  const where = `${group}/${step.id ?? '?'}`;
  if (typeof step.id !== 'string' || step.id.includes(repeatSeparator)) {
    throw new Error(`${where}: every step needs an id without '${repeatSeparator}'`);
  }
  const isRequest = step.request !== undefined;
  if (isRequest === (step.action !== undefined)) {
    throw new Error(`${where}: a step is either a request or an action`);
  }
  if (step.action !== undefined && !actions.includes(step.action)) {
    throw new Error(`${where}: unknown action ${step.action}`);
  }
  if (step.request !== undefined && !(step.request.method && step.request.path)) {
    throw new Error(`${where}: a request needs a method and a path`);
  }
}

/** Validates a whole group, ids included (they key the references). */
export function validateGroup(group) {
  const seen = new Set();
  for (const step of group.steps) {
    validateStep(step, group.name);
    if (seen.has(step.id)) {
      throw new Error(`${group.name}/${step.id}: duplicate step id`);
    }
    seen.add(step.id);
  }
  return group;
}

/**
 * Reads every group file, in name order, optionally filtered by group name.
 * @returns {{ name: string, title: string, steps: object[] }[]}
 */
export function loadGroups(dir, only) {
  return fs.readdirSync(dir)
    .filter((file) => file.endsWith('.json'))
    .sort()
    .map((file) => ({ name: path.basename(file, '.json'), file: path.join(dir, file) }))
    .filter((entry) => !only || only.includes(entry.name))
    .map((entry) => {
      const content = JSON.parse(fs.readFileSync(entry.file, 'utf8'));
      return validateGroup({ name: entry.name, title: content.title, steps: content.steps });
    });
}

/** Unrolls `repeat: n` into n steps suffixed #1..#n. */
export function expandSteps(steps) {
  return steps.flatMap((step) => {
    if (!step.repeat) {
      return [step];
    }
    const { repeat, ...single } = step;
    return Array.from({ length: repeat }, (_, index) => ({
      ...single,
      id: `${step.id}${repeatSeparator}${index + 1}`,
    }));
  });
}

/** Replaces {{key:alias}} with the raw test key it names. */
export function resolveKeys(text, keys) {
  return text.replace(keyPlaceholder, (_, alias) => {
    if (!(alias in keys)) {
      throw new Error(`unknown key alias ${alias}`);
    }
    return keys[alias];
  });
}

/** Resolves the key placeholders of a request (path and header values). */
export function resolveRequest(request, keys) {
  const headers = Object.entries(request.headers ?? {})
    .map(([name, value]) => [name, resolveKeys(value, keys)]);
  return {
    method: request.method,
    path: resolveKeys(request.path, keys),
    headers: Object.fromEntries(headers),
  };
}
