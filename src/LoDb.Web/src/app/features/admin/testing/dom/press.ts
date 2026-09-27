import type { AdminVisit } from '../admin-visit';

/** Presses the button of `scope` that reads `label`, then renders the page. */
export function press(visit: AdminVisit, label: string, scope: ParentNode = visit.page): void {
  const buttons = [...scope.querySelectorAll<HTMLButtonElement>('button')];
  const button = buttons.find((candidate) => candidate.textContent?.trim() === label);
  if (!button) {
    throw new Error(`No button reads "${label}".`);
  }
  button.click();
  visit.harness.detectChanges();
}
