/** Fewest characters of a password, counted in code points like the server does. */
export const PASSWORD_MIN_LENGTH = 12;

/** A rule of the checklist, named by the code the API answers when it is broken. */
export interface PasswordRule {
  /** The API's code, such as `auth.password.rule_digit`, which is its translation key too. */
  readonly code: string;
  readonly met: boolean;
}

// The API's CnilPasswordValidator, rule by rule: `Rune.IsLower` and `Rune.IsUpper` are the
// Ll and Lu categories, the digit is an ASCII one, and a special character is anything that
// is neither a letter nor a number, whatever the script.
const CLASS_RULES: readonly (readonly [code: string, pattern: RegExp])[] = [
  ['auth.password.rule_lowercase', /\p{Ll}/u],
  ['auth.password.rule_uppercase', /\p{Lu}/u],
  ['auth.password.rule_digit', /[0-9]/],
  ['auth.password.rule_special', /[^\p{L}\p{N}]/u],
];

/**
 * The live checklist of a new password, in the server's order. The server stays the judge:
 * it also refuses the common passwords and those over 4096 bytes, which no checklist shows.
 */
export function passwordRules(password: string): PasswordRule[] {
  return [
    // Spread to count code points, not UTF-16 units: an emoji is one character.
    { code: 'auth.password.rule_length', met: [...password].length >= PASSWORD_MIN_LENGTH },
    ...CLASS_RULES.map(([code, pattern]) => ({ code, met: pattern.test(password) })),
  ];
}

/** Both fields agree, and there is something to agree on. */
export function passwordsMatch(password: string, confirmation: string): boolean {
  return password !== '' && password === confirmation;
}
