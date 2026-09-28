import type { JsonLdNode } from '../core/json-ld-node';
import { plainText } from '../core/plain-text';
import { pruneJsonLd } from '../core/prune-json-ld';
import { SCHEMA_ORG } from '../core/schema-org';

/** The page that describes the project itself. */
export function aboutPage(page: {
  readonly name: string;
  readonly url: string;
  readonly description?: string;
  readonly inLanguage?: string;
}): JsonLdNode {
  return pruneJsonLd({
    '@context': SCHEMA_ORG,
    '@type': 'AboutPage',
    '@id': `${page.url}#about`,
    url: page.url,
    name: page.name,
    description: plainText(page.description),
    inLanguage: page.inLanguage,
  });
}
