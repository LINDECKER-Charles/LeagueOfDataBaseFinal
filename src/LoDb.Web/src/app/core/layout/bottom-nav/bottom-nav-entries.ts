import type { NavEntry } from '../shell/nav-entry';

/** Six thumb-zone destinations, labels always visible (Material and HIG guidance). */
export const BOTTOM_NAV_ENTRIES: readonly NavEntry[] = [
  { path: '', label: 'nav.short.home', icon: 'home' },
  { path: 'champions', label: 'nav.short.champions', icon: 'champion' },
  { path: 'items', label: 'nav.short.items', icon: 'item' },
  { path: 'runes', label: 'nav.short.runes', icon: 'rune' },
  { path: 'summoners', label: 'nav.short.summoners', icon: 'spell' },
  { path: 'trends', label: 'community.nav.trends', icon: 'trends' },
];
