import type { NavEntry } from '../shell/nav-entry';

/** The four data sections the header groups under its "Codex" menu. */
export const CODEX_ENTRIES: readonly NavEntry[] = [
  { path: 'champions', label: 'header.navigation.champion' },
  { path: 'items', label: 'header.navigation.item' },
  { path: 'runes', label: 'header.navigation.runes' },
  { path: 'summoners', label: 'header.navigation.summoner' },
];
