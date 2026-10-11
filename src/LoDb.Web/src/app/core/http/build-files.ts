/**
 * The JSON files of the browser build the SSR server read once, at start-up: the i18n
 * catalogues and the changelog, by path relative to the build (`i18n/fr.json`), parsed and
 * frozen (`readBuildFiles` in src/server/assets).
 */
export type BuildFiles = ReadonlyMap<string, unknown>;
