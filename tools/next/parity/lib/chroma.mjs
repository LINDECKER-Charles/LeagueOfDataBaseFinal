// Chroma labels of the legacy export, derived by the legacy front's own rule.
import path from 'node:path';
import { pathToFileURL } from 'node:url';
import { repoRoot } from './settings.mjs';

const legacyRule = 'legacy/app/assets/vue/chroma/chromaLabel.ts';

/**
 * The label function the legacy pages run (a TypeScript module Node loads as is). Only
 * read, never copied: the comparison follows the rule the legacy stack actually ships.
 */
export async function legacyChromaLabel() {
  const module = await import(pathToFileURL(path.join(repoRoot, legacyRule)).href);
  return module.chromaLabel;
}

/**
 * Replaces the colours the PHP export keeps on each chroma with the label derived from
 * them, the form `catalog export` writes. The projection is changed in place.
 */
export function labelChromas(projection, label) {
  for (const champion of projection.champions?.entries ?? []) {
    for (const skin of champion.skins ?? []) {
      skin.chromas = skin.chromas.map(({ id, name, colors }) => ({
        id,
        name,
        label: label({ name, colors }),
      }));
    }
  }
  return projection;
}
