/** A JSON object, as opposed to an array, a scalar or `null`. */
export function isJsonRecord(value: unknown): value is Readonly<Record<string, unknown>> {
  return typeof value === 'object' && value !== null && !Array.isArray(value);
}
