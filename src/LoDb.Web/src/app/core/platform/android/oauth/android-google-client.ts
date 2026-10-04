/** The Google client of the app's sign-in and the App Link Google redirects to. */
export interface AndroidGoogleClient {
  /** Null when the build names no Google client: the sign-in is then unavailable. */
  readonly clientId: string | null;
  /** `https://{App Link host}/app/oauth/google`, registered with the Google client. */
  readonly redirectUri: string;
}
