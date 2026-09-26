/**
 * Where the verification of an address stands: its link being checked, then its answer, or
 * `waiting` on a page opened without a link.
 */
export type VerifyOutcome = 'waiting' | 'verifying' | 'verified' | 'already' | 'invalid' | 'failed';
