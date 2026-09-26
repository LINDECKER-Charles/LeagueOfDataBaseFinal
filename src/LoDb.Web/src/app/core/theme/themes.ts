/**
 * The four visual identities (legacy App\Service\Client\Theme). The inline script of
 * src/index.html repeats this list, since it runs before any bundle; the theme-from-cookie
 * spec keeps both in step.
 */
export const THEMES = ['hextech', 'zaun', 'noxus', 'spirit-blossom'] as const;

export type Theme = (typeof THEMES)[number];
