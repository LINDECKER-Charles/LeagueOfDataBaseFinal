const PATH_CLASSES: Readonly<Record<string, string>> = {
  Precision: 'path-precision',
  Domination: 'path-domination',
  Sorcery: 'path-sorcery',
  Resolve: 'path-resolve',
  Inspiration: 'path-inspiration',
};

/**
 * The class that tints a shared build with its primary path, keyed on the path's key, which
 * no language changes; Precision's gold for a ghost or a path Riot adds later.
 */
export function pathClassOf(key: string | null | undefined): string {
  return PATH_CLASSES[key ?? ''] ?? PATH_CLASSES['Precision'];
}
