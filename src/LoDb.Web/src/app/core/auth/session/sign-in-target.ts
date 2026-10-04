/** Where a sign-in lands: the requested page when it is safe, the fallback otherwise. */
export interface SignInTarget {
  /** The `returnUrl` query parameter of the login page: untrusted. */
  readonly returnUrl: string | null | undefined;
  /** Root-relative, such as `/fr/account/profile`. */
  readonly fallbackUrl: string;
}
