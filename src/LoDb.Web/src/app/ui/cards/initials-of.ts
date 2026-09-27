const INITIALS_LENGTH = 2;

/** What stands in for an absent image: the first letters of the name, in capitals. */
export function initialsOf(name: string): string {
  return [...name.trim()].slice(0, INITIALS_LENGTH).join('').toLocaleUpperCase();
}
