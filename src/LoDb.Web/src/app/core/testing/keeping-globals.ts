/**
 * Runs a server render inside a spec and gives jsdom its DOM classes back afterwards.
 *
 * The server platform emulates a DOM by writing domino's classes over the globals (Event,
 * Node…), and Vitest runs the spec files of a worker in one global scope: the specs that run
 * next would build domino events for jsdom elements, which jsdom refuses to dispatch. Vitest
 * serves jsdom's classes through accessors, so their values are read and written back through
 * them, which also restores the jsdom window behind.
 */
export async function keepingGlobals<T>(render: () => Promise<T>): Promise<T> {
  const classes = Object.entries(Object.getOwnPropertyDescriptors(globalThis))
    .filter(([name]) => /^[A-Z]/.test(name))
    .map(([name, descriptor]) => [name, descriptor.get?.call(globalThis) ?? descriptor.value]);
  try {
    return await render();
  } finally {
    for (const [name, value] of classes) {
      if (Reflect.get(globalThis, name) !== value) {
        Reflect.set(globalThis, name, value);
      }
    }
  }
}
