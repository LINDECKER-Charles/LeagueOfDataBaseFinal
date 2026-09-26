/**
 * How an identity presents itself in the picker, and the colour it gives the browser chrome
 * (Android tab strip, iOS status bar): its own deepest surface, so the page melts into it.
 */
export interface ThemeIdentity {
  /** Proper name, the same in every locale. */
  readonly label: string;
  /** City of Runeterra the identity comes from, a proper name as well. */
  readonly origin: string;
  /** Value of `<meta name="theme-color">`, the identity's `--color-hextech-black`. */
  readonly browserColor: string;
}
