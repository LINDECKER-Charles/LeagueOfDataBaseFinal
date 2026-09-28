// Past this length the header chip would push the other controls: the name is cut.
const SHORT_NAME_MAX = 14;
const ELLIPSIS = '…';

/** A summoner name short enough for the header chip, cut with an ellipsis past 14. */
export function shortName(username: string): string {
  const characters = [...username];
  return characters.length > SHORT_NAME_MAX
    ? characters.slice(0, SHORT_NAME_MAX - 1).join('') + ELLIPSIS
    : username;
}
