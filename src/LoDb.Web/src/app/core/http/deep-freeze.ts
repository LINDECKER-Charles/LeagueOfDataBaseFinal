/**
 * Freezes `value` and everything it holds, then returns it: data one process shares between
 * every render (catalogues, `/api/meta`) can then be read by all of them but changed by none.
 */
export function deepFreeze<T>(value: T): T {
  if (value !== null && typeof value === 'object' && !Object.isFrozen(value)) {
    Object.freeze(value);
    for (const child of Object.values(value)) {
      deepFreeze(child);
    }
  }
  return value;
}
