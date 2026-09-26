import { appendInstallLinks } from './append-install-links';
import { WORKER_SCRIPT_URL } from './worker-script-url';

/**
 * Makes the site installable and registers its worker once the page has loaded, so that the
 * worker's first fetches never compete with the page's. A refusal (private browsing, an
 * insecure origin, storage disabled) leaves the site as it is, without a worker.
 */
export function registerWorker(view: Window): void {
  appendInstallLinks(view.document);
  if (!('serviceWorker' in view.navigator)) {
    return;
  }
  const container = view.navigator.serviceWorker;
  const register = (): void => {
    // updateViaCache 'none': a deploy reaches every visitor on their next navigation.
    container
      .register(WORKER_SCRIPT_URL, { scope: '/', updateViaCache: 'none' })
      .catch(() => undefined);
  };
  if (view.document.readyState === 'complete') {
    register();
  } else {
    view.addEventListener('load', register, { once: true });
  }
}
