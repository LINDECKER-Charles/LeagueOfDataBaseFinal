// A figure as a message writes it, group separators included: "865", "1,234", "1 234".
const FIGURE = /\d(?:[\d.,'\u00a0\u202f ]*\d)?/g;
const NOT_A_DIGIT = /\D/g;

/**
 * A translated count ("173 results") cut around its figure, so the figure and the words can
 * be set apart. The figure is found by its digits, whatever separators the locale groups
 * them with; when the message holds no such figure, it all stays in `before`.
 */
export function splitAroundCount(
  text: string,
  count: number,
): { readonly before: string; readonly figure: string | null; readonly after: string } {
  const digits = String(count);
  for (const match of text.matchAll(FIGURE)) {
    if (match[0].replace(NOT_A_DIGIT, '') === digits) {
      const end = match.index + match[0].length;
      return {
        before: text.slice(0, match.index).trim(),
        figure: match[0],
        after: text.slice(end).trim(),
      };
    }
  }
  return { before: text.trim(), figure: null, after: '' };
}
