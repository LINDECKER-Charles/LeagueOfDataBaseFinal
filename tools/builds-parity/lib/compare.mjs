// The differences between the facts of a page on the two stacks, field by field.

import { modeOf } from './modes.mjs';

/** The facts of a page, the mode's label read as its mode. */
export function normalize(facts, labels) {
  return facts === null ? null : { ...facts, mode: modeOf(facts.mode, labels) };
}

function walk(legacy, next, path, out) {
  const plain = (value) => typeof value !== 'object' || value === null;
  if (plain(legacy) || plain(next)) {
    if (!Object.is(legacy, next)) out.push({ field: path, legacy, next });
    return;
  }
  const keys = new Set([...Object.keys(legacy), ...Object.keys(next)]);
  for (const key of keys) {
    walk(legacy[key], next[key], Array.isArray(legacy) ? `${path}[${key}]` : `${path}.${key}`, out);
  }
}

/**
 * The differences of one page: its HTTP status first; then, when both show the build, each
 * field that differs (`$.steps[1].items[0].name`), with both values.
 */
export function compareReadings(legacy, next) {
  if (legacy.status !== next.status) {
    return [{ field: 'status', legacy: legacy.status, next: next.status }];
  }
  const out = [];
  walk(legacy.facts, next.facts, '$', out);
  return out;
}
