/**
 * "%key%" substitution shared by every island label coming from the Symfony
 * catalogs. Uses split/join rather than String.replace so EVERY occurrence of a
 * placeholder is substituted — a plain replace() with a string needle only
 * swaps the first one, which silently truncates repeated placeholders.
 */
export function formatTemplate(template: string, params: Record<string, string | number>): string {
    return Object.entries(params).reduce(
        (out, [key, value]) => out.split(`%${key}%`).join(String(value)),
        template,
    )
}

/** The singular wording for exactly one, the plural one otherwise (no ICU on the client). */
export function pluralTemplate(count: number, one: string, many: string): string {
    return count === 1 ? one : many
}

/**
 * The text around ONE placeholder, for markup that styles the value apart
 * from its label ("<b>12</b> results"). A template without the placeholder
 * keeps all its text before an empty value.
 */
export function splitTemplate(template: string, key: string): { before: string; after: string } {
    const [before, ...rest] = template.split(`%${key}%`)
    return { before, after: rest.join(`%${key}%`) }
}
