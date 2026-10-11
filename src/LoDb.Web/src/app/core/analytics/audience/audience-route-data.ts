/**
 * How the audience sees a route, set in its `data` and inherited by its children. A route
 * without it is sent as its template (`/fr/u/:username`, `/b/:token`), never as its address.
 */
export const AUDIENCE_ROUTE_DATA = {
  key: 'audience',
  /** Lists and details, whose ids are public and feed the trends: their real path is sent. */
  path: { audience: 'path' },
  /** Account and admin pages: nothing at all is sent. */
  none: { audience: 'none' },
} as const;
