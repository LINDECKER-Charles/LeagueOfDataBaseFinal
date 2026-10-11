/** What the policy of a rendered page makes of its inline scripts. */
export interface PageScripts {
  /** The CSP sources (`'sha256-…'`) of the scripts it admits, in document order. */
  readonly hashes: readonly string[];
  /** How many inline scripts the page holds that it leaves out: the browser runs none. */
  readonly unexpected: number;
}
