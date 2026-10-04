import type { JsonLdNode } from '../core/json-ld-node';
import { plainText } from '../core/plain-text';
import { pruneJsonLd } from '../core/prune-json-ld';
import { SCHEMA_ORG } from '../core/schema-org';

/**
 * A public profile: the Person it is about. The owner's builds ride along as a separate
 * {@link itemList} node, never nested here.
 */
export function profilePage(profile: {
  readonly name: string;
  readonly url: string;
  readonly image?: string | null;
  readonly description?: string | null;
}): JsonLdNode {
  return {
    '@context': SCHEMA_ORG,
    '@type': 'ProfilePage',
    mainEntity: pruneJsonLd({
      '@type': 'Person',
      name: profile.name,
      url: profile.url,
      image: profile.image,
      description: plainText(profile.description),
    }),
  };
}
