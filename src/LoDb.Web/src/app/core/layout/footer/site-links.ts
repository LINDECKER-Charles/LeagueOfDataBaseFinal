import type { NavEntry } from '../shell/nav-entry';

/**
 * Pages of the site listed by the footer. The about and api labels live in their own
 * catalogue scopes, hence the prefixed keys.
 */
export const SITE_LINKS: readonly NavEntry[] = [
  { path: '', label: 'footer.navigation.home' },
  { path: 'champions', label: 'footer.navigation.champion' },
  { path: 'items', label: 'footer.navigation.item' },
  { path: 'runes', label: 'footer.navigation.runes' },
  { path: 'summoners', label: 'footer.navigation.summoner' },
  { path: 'about', label: 'about.index.title' },
  { path: 'about/data', label: 'about.data.title' },
  { path: 'faq', label: 'about.faq.title' },
  { path: 'donate', label: 'nav.donate' },
  { path: 'developers', label: 'api.nav.developers' },
];
