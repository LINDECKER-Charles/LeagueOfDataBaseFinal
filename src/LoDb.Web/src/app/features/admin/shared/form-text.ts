/**
 * The text of a field of a submitted form, the browser's own submission cancelled: the
 * forms of the admin are native ones, read once when sent. '' when there is no such field.
 */
export function formText(event: Event, name: string): string {
  event.preventDefault();
  const value = new FormData(event.target as HTMLFormElement).get(name);
  return typeof value === 'string' ? value : '';
}
