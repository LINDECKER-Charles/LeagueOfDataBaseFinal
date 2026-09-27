import type { AdminVisit } from '../admin-visit';

/** Renders the page once the answers it was given are adopted. */
export async function settle(visit: AdminVisit): Promise<void> {
  await visit.harness.fixture.whenStable();
  visit.harness.detectChanges();
}
