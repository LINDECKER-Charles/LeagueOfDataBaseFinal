/** What happened around an open disclosure: a press, a key, or a completed navigation. */
export type DisclosureCue =
  | { readonly kind: 'pointer'; readonly inside: boolean }
  | { readonly kind: 'key'; readonly key: string }
  | { readonly kind: 'navigation' };

/**
 * Whether a header disclosure must fold. A press outside it, Escape, or a navigation folds
 * an open one; nothing ever folds a closed one, so a closed menu costs no work per event.
 */
export function disclosureClosing(open: boolean, cue: DisclosureCue): boolean {
  if (!open) {
    return false;
  }
  switch (cue.kind) {
    case 'pointer':
      return !cue.inside;
    case 'key':
      return cue.key === 'Escape';
    case 'navigation':
      return true;
  }
}
