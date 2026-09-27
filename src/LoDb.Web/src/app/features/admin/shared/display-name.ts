/**
 * How an account is named in the admin, as the legacy `displayName`: its username, then its
 * Riot tagline after a `#` when it set one ("Hexanti#EUW").
 */
export function displayName(username: string, tagline: string | null | undefined): string {
  return tagline ? `${username}#${tagline}` : username;
}
