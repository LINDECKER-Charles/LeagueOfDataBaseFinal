// Once loaded, a busy page still gets its task within this delay.
const IDLE_TIMEOUT_MS = 2000;
// Where requestIdleCallback is missing (Safari): a short pause after the load event.
const IDLE_FALLBACK_MS = 200;

/**
 * Runs `task` once the page has loaded (its LCP image included) and the main thread is idle,
 * so that what the task fetches never competes with the page's first paint; returns the way
 * to cancel it. A page shown after the load (a navigation in the app) only waits for idle.
 */
export function whenIdleAfterLoad(view: Window, task: () => void): () => void {
  let cancelIdle: () => void = () => undefined;
  const onLoad = (): void => {
    cancelIdle = whenIdle(view, task);
  };
  if (view.document.readyState === 'complete') {
    onLoad();
  } else {
    view.addEventListener('load', onLoad, { once: true });
  }
  return () => {
    view.removeEventListener('load', onLoad);
    cancelIdle();
  };
}

function whenIdle(view: Window, task: () => void): () => void {
  if ('requestIdleCallback' in view) {
    const handle = view.requestIdleCallback(task, { timeout: IDLE_TIMEOUT_MS });
    return () => view.cancelIdleCallback(handle);
  }
  const handle = setTimeout(task, IDLE_FALLBACK_MS);
  return () => clearTimeout(handle);
}
