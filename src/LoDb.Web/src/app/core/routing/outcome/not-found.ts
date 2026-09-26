import type { PageOutcome } from './page-outcome';

/** The real 404 of ADR 0005, answered by every URL the site does not serve. */
export const NOT_FOUND: PageOutcome = { kind: 'not-found' };
