/** A non-empty string, or `null` for anything else: missing, empty or of another type. */
export function jsonText(value: unknown): string | null {
  return typeof value === 'string' && value.trim() !== '' ? value : null;
}
