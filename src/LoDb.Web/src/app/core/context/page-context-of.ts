import type { CatalogMeta } from '../api/generated/models/catalog-meta';
import { languageOf } from '../api/meta/language-of';
import { QueryString } from '../routing/url/query-string';
import type { ContextSources } from './context-sources';
import type { PageContext } from './page-context';

const VERSION_PARAM = 'version';
const LANG_PARAM = 'lang';
const REGION_SEPARATOR = '_';

// `en_GB` and `en_US` share `en`: a variant never swaps the language of the locale.
function primaryOf(language: string): string {
  return language.split(REGION_SEPARATOR, 1)[0];
}

function firstOf(candidates: (string | null | undefined)[], accepts: (value: string) => boolean) {
  return candidates.find((candidate): candidate is string => !!candidate && accepts(candidate));
}

/**
 * The context of a page, each axis taken from the strongest source that names a value
 * `/api/meta` lists (ADR 0005: path > query > cookie): the version defaults to the latest,
 * the language to the locale's own; a language is kept only as a regional variant of the
 * locale's (`en_GB` on `/en/`, never `ja_JP` on `/fr/`). Null while no version is ingested.
 */
export function pageContextOf(sources: ContextSources, meta: CatalogMeta): PageContext | null {
  const query = QueryString.parse(sources.query);
  const { remembered } = sources;
  const candidates = [sources.path, query.get(VERSION_PARAM), remembered?.version];
  const version = firstOf(candidates, (value) => meta.versions.includes(value)) ?? meta.latest;
  if (!version) {
    return null;
  }
  const own = languageOf(meta, sources.locale);
  const language = firstOf(
    [query.get(LANG_PARAM), remembered?.lang],
    (value) => meta.languages.includes(value) && primaryOf(value) === primaryOf(own),
  );
  const pinned = version !== meta.latest;
  return { locale: sources.locale, version, pinned, language: language ?? own };
}
