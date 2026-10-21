// Two segments at least, not glued to another digit or point: `16.18.1` in a line, a list, a
// LogsQL row (`16.18.1\t12034`, whose count has no point) or a JSON document.
const VERSION = /(?<![\d.])\d+(?:\.\d+)+(?![\d.])/g;

/**
 * The versions in a pasted text (§ 3.3.7), in their order, each once: the first occurrence
 * keeps its rank.
 */
export function versionTokens(text: string): string[] {
  return [...new Set(text.match(VERSION) ?? [])];
}
