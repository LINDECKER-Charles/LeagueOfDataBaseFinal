// The game modes by their labels, in every locale of the site: the pages show a label, in a
// language that differs between the stacks (the visitor's on the legacy one, the build's on
// the rewrite), and the comparison wants the mode. The labels come from the root catalogues
// of src/LoDb.Web/public/i18n, converted from the legacy ones (L3.3).

import { readdirSync, readFileSync } from 'node:fs';
import { join } from 'node:path';

/** Label → the modes it names, from every `<locale>.json` of the directory. */
export function modeLabels(directory) {
  const labels = new Map();
  for (const file of readdirSync(directory).filter((name) => name.endsWith('.json'))) {
    const catalogue = JSON.parse(readFileSync(join(directory, file), 'utf8'));
    for (const [mode, label] of Object.entries(catalogue.build?.mode ?? {})) {
      const modes = labels.get(label) ?? new Set();
      labels.set(label, modes.add(mode));
    }
  }
  return labels;
}

/** The mode a label names, or the label itself, marked, when it names none or several. */
export function modeOf(label, labels) {
  const modes = [...(labels.get(label) ?? [])];
  return modes.length === 1 ? modes[0] : `?${label}`;
}
