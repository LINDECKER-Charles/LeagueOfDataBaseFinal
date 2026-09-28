/** How the author of a build is named: its username, then its Riot tag line after a #. */
export function displayName(username: string, riotTagline: string | null | undefined): string {
  return riotTagline ? `${username}#${riotTagline}` : username;
}
