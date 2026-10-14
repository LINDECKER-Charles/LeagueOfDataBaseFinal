import { isPlatformBrowser } from '@angular/common';
import { DOCUMENT, Injectable, PLATFORM_ID, computed, inject, signal } from '@angular/core';

// A choice of this browser, kept until the visitor changes it; never sent anywhere.
const OPT_OUT_KEY = 'lodb.audience.opt-out';
const OPTED_OUT = '1';

/** Global Privacy Control (`Sec-GPC`), not yet in TypeScript's DOM types. */
interface PrivacySignals {
  readonly globalPrivacyControl?: boolean;
}

/**
 * Whether this browser's visits are measured. The visitor opts out from the privacy policy
 * (kept in `localStorage`, on this browser only), and a browser that sends Global Privacy
 * Control is never measured, whatever the stored choice. On the server, nothing is measured,
 * nor in a browser driven by automation (`navigator.webdriver`).
 */
@Injectable({ providedIn: 'root' })
export class AudienceOptOut {
  private readonly document = inject(DOCUMENT);
  private readonly inBrowser = isPlatformBrowser(inject(PLATFORM_ID));
  private readonly optedOut = signal(this.readChoice());

  /** The browser asks every site not to measure it: followed, and not overridable here. */
  readonly hasGlobalPrivacyControl = this.readGlobalPrivacyControl();

  /** Whether this browser refuses the measure, by the visitor's choice or by GPC. */
  readonly isRefused = computed(() => this.hasGlobalPrivacyControl || this.optedOut());

  // Crawlers that run the scripts would count as visitors: no visitor, so no choice to show.
  private readonly isAutomated = this.document.defaultView?.navigator.webdriver === true;

  /** Whether the visits of this browser are counted: never on the server nor by a robot. */
  readonly isMeasured = computed(() => this.inBrowser && !this.isAutomated && !this.isRefused());

  /** Records the visitor's choice for this browser. */
  choose(measured: boolean): void {
    try {
      const storage = this.storage();
      if (measured) {
        storage?.removeItem(OPT_OUT_KEY);
      } else {
        storage?.setItem(OPT_OUT_KEY, OPTED_OUT);
      }
    } catch {
      // Storage refused (private mode, quota): the choice holds until the page reloads.
    }
    this.optedOut.set(!measured);
  }

  private readChoice(): boolean {
    try {
      return this.storage()?.getItem(OPT_OUT_KEY) === OPTED_OUT;
    } catch {
      return false;
    }
  }

  private readGlobalPrivacyControl(): boolean {
    const navigator = this.document.defaultView?.navigator as PrivacySignals | undefined;
    return navigator?.globalPrivacyControl === true;
  }

  private storage(): Storage | null {
    return this.inBrowser ? (this.document.defaultView?.localStorage ?? null) : null;
  }
}
