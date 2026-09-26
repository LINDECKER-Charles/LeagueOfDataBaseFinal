/**
 * The fields of a submitted form, the browser's own submission cancelled: the forms of the
 * account pages are native ones, read once when sent.
 */
export function submittedForm(event: Event): FormData {
  event.preventDefault();
  return new FormData(event.target as HTMLFormElement);
}
