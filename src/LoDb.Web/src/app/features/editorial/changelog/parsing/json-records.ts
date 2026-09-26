import { isJsonRecord } from './is-json-record';

/** The objects of a JSON array; anything else reads as an empty list. */
export function jsonRecords(value: unknown): readonly Readonly<Record<string, unknown>>[] {
  return Array.isArray(value) ? value.filter(isJsonRecord) : [];
}
