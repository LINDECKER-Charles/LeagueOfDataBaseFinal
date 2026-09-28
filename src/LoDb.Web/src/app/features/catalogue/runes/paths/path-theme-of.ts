// Keyed on the path's key, the same in every language, unlike its name.
const THEMES: Readonly<Record<string, string>> = {
  Precision: 'path-precision',
  Domination: 'path-domination',
  Sorcery: 'path-sorcery',
  Resolve: 'path-resolve',
  Inspiration: 'path-inspiration',
};

/**
 * The class tinting a rune path's page in its colour (rune-detail.css). A path Riot adds
 * later takes the first one's colour rather than none, as the legacy site did.
 */
export function pathThemeOf(key: string): string {
  return THEMES[key] ?? 'path-precision';
}
