import type { UrlTree } from '@angular/router';
import type { CanonicalLink } from '../../../../../../core/api/generated/models/canonical-link';
import type { PageContext } from '../../../../../../core/context/page-context';
import { injectCatalogueLink } from '../../links/inject-catalogue-link';

/**
 * The link to a page the API names (`{ version, path }`, the version null for the latest
 * one's short URL), in the reader's locale and regional variant: a state's canonical, an
 * opener, the next state.
 */
export function injectStateLink(): (context: PageContext, link: CanonicalLink) => UrlTree {
  const linkTo = injectCatalogueLink();
  return (context, link) =>
    linkTo(
      { ...context, version: link.version ?? context.version, pinned: link.version !== null },
      link.path,
    );
}
