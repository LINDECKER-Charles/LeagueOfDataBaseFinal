/**
 * Client mirror of the server-side CNIL constraint (App\Validator\CnilPassword):
 * >= 12 characters plus one lowercase, one uppercase, one digit and one special
 * character — "special" being anything that is neither a letter nor a digit,
 * Unicode-wide, exactly like the PHP patterns. The server stays the source of
 * truth; this module only powers the reactive checklist.
 */
export const PASSWORD_MIN_LENGTH = 12

export type PasswordRuleId = 'length' | 'lowercase' | 'uppercase' | 'digit' | 'special'

export interface PasswordRuleState {
    id: PasswordRuleId
    satisfied: boolean
}

const CLASS_PATTERNS: ReadonlyArray<[PasswordRuleId, RegExp]> = [
    ['lowercase', /\p{Ll}/u],
    ['uppercase', /\p{Lu}/u],
    ['digit', /[0-9]/],
    ['special', /[^\p{L}\p{N}]/u],
]

export function evaluatePasswordRules(password: string): PasswordRuleState[] {
    return [
        // Spread to count code points, matching PHP's mb_strlen (not UTF-16 units).
        { id: 'length', satisfied: [...password].length >= PASSWORD_MIN_LENGTH },
        ...CLASS_PATTERNS.map(([id, pattern]) => ({ id, satisfied: pattern.test(password) })),
    ]
}

/** Both fields agree AND there is something to agree on. */
export function passwordsMatch(password: string, confirmation: string): boolean {
    return password !== '' && password === confirmation
}
