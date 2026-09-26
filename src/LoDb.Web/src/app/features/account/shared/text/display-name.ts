/** How a summoner is named on its profile: its username, then its Riot tag line after a #. */
export function displayName(username: string, riotTagline: string | null): string {
  return riotTagline === null ? username : `${username}#${riotTagline}`;
}
