import type { NavEntry } from '../shell/nav-entry';

/**
 * Pages of the site listed by the footer. The about and api labels live in their own
 * catalogue scopes, hence the prefixed keys; the build lends them to the root catalogue, and
 * a new one is added to src/LoDb.Web/i18n/chrome-keys.json. The patch history links every
 * past patch, which the version selector, a form, does not offer to crawlers.
 */
export const SITE_LINKS: readonly NavEntry[] = [
  { path: '', label: 'footer.navigation.home' },
  { path: 'champions', label: 'footer.navigation.champion' },
  { path: 'items', label: 'footer.navigation.item' },
  { path: 'runes', label: 'footer.navigation.runes' },
  { path: 'summoners', label: 'footer.navigation.summoner' },
  { path: 'patches', label: 'footer.navigation.patches' },
  { path: 'about', label: 'about.index.title' },
  { path: 'about/data', label: 'about.data.title' },
  { path: 'faq', label: 'about.faq.title' },
  { path: 'donate', label: 'nav.donate' },
  { path: 'developers', label: 'api.nav.developers' },
];
