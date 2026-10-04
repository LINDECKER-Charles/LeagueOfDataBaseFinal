const ISO_DATE = /^(\d{4})-(\d{2})-(\d{2})$/;

/**
 * A release date as the changelog has always printed it, `dd/mm/yyyy`, in every locale: the
 * release texts are French. A date in another shape is shown as written.
 */
export function formatReleaseDate(date: string): string {
  const parts = ISO_DATE.exec(date);
  return parts === null ? date : `${parts[3]}/${parts[2]}/${parts[1]}`;
}
