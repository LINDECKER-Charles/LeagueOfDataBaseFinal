import type { BrowserContext } from '@playwright/test';

/** The identities of the site (src/index.html); the first one is the default. */
export const THEMES = ['hextech', 'zaun', 'noxus', 'spirit-blossom'] as const;

export type Theme = (typeof THEMES)[number];

// Read by the inline script of the document before the first frame.
const THEME_COOKIE = 'lod_theme';

/** Chooses the theme of every page of the context, as the theme picker does. */
export async function useTheme(context: BrowserContext, baseUrl: string, theme: Theme) {
  await context.addCookies([{ name: THEME_COOKIE, value: theme, url: baseUrl }]);
}
