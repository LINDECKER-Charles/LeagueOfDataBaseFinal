import { SITE_IDENTITY } from '../../site-identity';
import type { JsonLdNode } from '../core/json-ld-node';
import { pruneJsonLd } from '../core/prune-json-ld';
import { SCHEMA_ORG } from '../core/schema-org';
import type { SiteGraphContext } from './site-graph-context';

/**
 * WebSite, Organization and the current WebPage in one `@graph`, linked by `@id`: consumers
 * resolve `publisher` and `isPartOf` into a single entity for the site, where three loose
 * nodes repeating a name read as unrelated fragments. No SearchAction: the search lives in
 * the lists' filter, and no URL answers a query server-side.
 */
export function siteGraph(context: SiteGraphContext): JsonLdNode {
  const root = `${context.urls.origin}/`;
  const website = `${root}#website`;
  const organization = `${root}#organization`;
  const shared = { name: SITE_IDENTITY.name, url: root, description: context.siteDescription };
  return {
    '@context': SCHEMA_ORG,
    '@graph': [
      pruneJsonLd({
        '@type': 'WebSite',
        '@id': website,
        ...shared,
        inLanguage: context.inLanguage,
        publisher: { '@id': organization },
      }),
      pruneJsonLd({
        '@type': 'Organization',
        '@id': organization,
        ...shared,
        logo: {
          '@type': 'ImageObject',
          '@id': `${root}#logo`,
          url: context.urls.absolute(SITE_IDENTITY.logoPath),
        },
        sameAs: [...SITE_IDENTITY.sameAs],
      }),
      webPage(context, website),
    ],
  };
}

function webPage(context: SiteGraphContext, website: string): JsonLdNode {
  return pruneJsonLd({
    '@type': 'WebPage',
    '@id': `${context.urls.canonical}#webpage`,
    url: context.urls.canonical,
    name: context.pageName,
    isPartOf: { '@id': website },
    inLanguage: context.inLanguage,
  });
}
