import type { PageOutcome } from './page-outcome';

/**
 * Carried in `extras.info` by the navigation that renders an outcome in place of its page,
 * at the same URL: what lets `isOutcomeNavigation` pick the error route for it alone.
 */
export class NavigationOutcome {
  constructor(readonly outcome: PageOutcome) {}
}
