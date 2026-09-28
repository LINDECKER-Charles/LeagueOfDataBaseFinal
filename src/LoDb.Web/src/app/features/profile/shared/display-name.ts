/**
 * How a summoner is named on its card: its username, then its Riot tag line after a #. The
 * account pages have their copy; a feature cannot share it.
 */
export function displayName(username: string, riotTagline: string | null): string {
  return riotTagline === null ? username : `${username}#${riotTagline}`;
}
