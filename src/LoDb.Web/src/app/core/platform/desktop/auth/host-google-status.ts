/** Where the last Google sign-in stands, as `GET /desktop/auth/session` answers it. */
export type HostGoogleStatus =
  | { readonly stage: 'pending' }
  | { readonly stage: 'succeeded' }
  | { readonly stage: 'failed'; readonly failure: string };
