import type { CatalogMeta } from '../../../core/api/generated/models/catalog-meta';
import { languageOf } from '../../../core/api/meta/language-of';
import type { PageContext } from '../../../core/context/page-context';
import type { HomeLink } from '../data/home-link';

const LANG_PARAM = 'lang';

/**
 * The links of the home in its context (ADR 0005): below `/{locale}/`, the version in the
 * path when an older one is pinned, `?lang=` when the page reads a regional variant. So a
 * visitor who switched the home keeps their choice one click further.
 */
export function linkMakerOf(context: PageContext, meta: CatalogMeta): (path: string) => HomeLink {
  const version = context.pinned ? `${context.version}/` : '';
  const prefix = `/${context.locale}/${version}`;
  const isVariant = context.language !== languageOf(meta, context.locale);
  const query: Record<string, string> = isVariant ? { [LANG_PARAM]: context.language } : {};
  return (path) => ({ path: `${prefix}${path}`, query });
}
