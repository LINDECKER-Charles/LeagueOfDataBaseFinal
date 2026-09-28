import type { Router } from '@angular/router';
import { NavigationOutcome } from './navigation-outcome';
import type { PageOutcome } from './page-outcome';

/** The outcome the current navigation renders in place of its page, or null. */
export function pendingOutcome(router: Router): PageOutcome | null {
  const info = router.currentNavigation()?.extras.info;
  return info instanceof NavigationOutcome ? info.outcome : null;
}
