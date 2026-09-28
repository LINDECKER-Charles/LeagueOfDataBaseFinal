/**
 * Settles like `work`, unless `delayMs` pass first: then resolves `fallback`. The work goes on
 * in the background, where the caller may still keep its result.
 */
export function within<T>(work: Promise<T>, delayMs: number, fallback: T): Promise<T> {
  let timer: ReturnType<typeof setTimeout> | undefined;
  const late = new Promise<T>((resolve) => {
    timer = setTimeout(() => resolve(fallback), delayMs);
  });
  return Promise.race([work, late]).finally(() => clearTimeout(timer));
}
