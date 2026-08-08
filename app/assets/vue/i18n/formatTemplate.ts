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
