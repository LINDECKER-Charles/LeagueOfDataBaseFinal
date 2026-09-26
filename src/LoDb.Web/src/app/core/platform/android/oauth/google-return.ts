/** What Google handed back to the redirect URI: a code, or the error that replaced it. */
export interface GoogleReturn {
  readonly state: string | null;
  readonly code: string | null;
  readonly error: string | null;
}
