import { DOCUMENT, DestroyRef, Injectable, inject } from '@angular/core';
import { externalLinkTarget } from './external-link-target';

// The main button: the others do not follow a link in the WebView.
const MAIN_BUTTON = 0;

/**
 * Sends the links to other sites to the system browser. Photino's WebView would otherwise
 * load them in the app's own window, which has no address bar and no way back, with the
 * app's bridge still reachable from them. Listening while capturing sees the click before
 * the router or any component could take it.
 */
@Injectable({ providedIn: 'root' })
export class ExternalLinks {
  private readonly document = inject(DOCUMENT);
  private readonly destroyRef = inject(DestroyRef);

  /** Starts intercepting; each external link is handed to `open`, whose failure is ignored. */
  intercept(open: (url: string) => Promise<void>): void {
    const listener = (event: MouseEvent): void => {
      const url =
        event.button === MAIN_BUTTON
          ? externalLinkTarget(event.target, this.document.location.origin)
          : null;
      if (url !== null) {
        event.preventDefault();
        // A refused link stays unopened rather than taking over the app's window.
        open(url).catch(() => undefined);
      }
    };
    this.document.addEventListener('click', listener, { capture: true });
    this.destroyRef.onDestroy(() =>
      this.document.removeEventListener('click', listener, { capture: true }),
    );
  }
}
