/**
 * Flattens a catalogue into `path → message`, the dotted keys Transloco translates.
 * Insertion order is kept so reports list keys in catalogue order.
 */
export function flattenKeys(tree, prefix = '') {
  const flat = new Map();
  for (const [key, value] of Object.entries(tree)) {
    const path = prefix ? `${prefix}.${key}` : key;
    if (value !== null && typeof value === 'object') {
      for (const [childPath, message] of flattenKeys(value, path)) {
        flat.set(childPath, message);
      }
    } else {
      flat.set(path, value);
    }
  }
  return flat;
}
