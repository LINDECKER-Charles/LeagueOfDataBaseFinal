/**
 * The token endpoints the desktop host serves on the page's own origin (`DesktopRoutes` of
 * `LoDb.Desktop`): the tokens stay in the host, which adds them to the API requests it relays.
 */
export const HostAuthRoutes = {
  login: '/desktop/auth/login',
  logout: '/desktop/auth/logout',
  session: '/desktop/auth/session',
  google: '/desktop/auth/google',
} as const;
