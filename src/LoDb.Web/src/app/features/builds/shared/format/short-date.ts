const PAD = 2;

/**
 * The day a build was last saved, `26/09/2026`, as the legacy page wrote it whatever the
 * locale. In UTC, as the API dates: the server's render and the browser's agree.
 */
export function shortDate(iso: string): string {
  const date = new Date(iso);
  const day = String(date.getUTCDate()).padStart(PAD, '0');
  const month = String(date.getUTCMonth() + 1).padStart(PAD, '0');
  return `${day}/${month}/${date.getUTCFullYear()}`;
}
