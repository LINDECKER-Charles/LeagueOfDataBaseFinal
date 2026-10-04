/**
 * What the front knows of the session. `unknown` until `/api/account/me` has answered, and
 * always during a server render, which stays anonymous: a template shows neither the sign-in
 * link nor the account while it lasts, so the hydrated page does not flicker.
 */
export type SessionStatus = 'unknown' | 'anonymous' | 'authenticated';
