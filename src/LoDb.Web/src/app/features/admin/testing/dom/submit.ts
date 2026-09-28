import type { AdminVisit } from '../admin-visit';

/** Submits the form of `scope` with `fields` filled in, by their names. */
export function submit(
  visit: AdminVisit,
  fields: Readonly<Record<string, string>>,
  scope?: Element,
): void {
  const form = (scope ?? visit.page).querySelector('form');
  for (const [name, value] of Object.entries(fields)) {
    const field = form?.querySelector<HTMLInputElement | HTMLSelectElement>(`[name="${name}"]`);
    if (!field) {
      throw new Error(`No field is named "${name}".`);
    }
    field.value = value;
  }
  form?.dispatchEvent(new Event('submit', { cancelable: true }));
  visit.harness.detectChanges();
}
