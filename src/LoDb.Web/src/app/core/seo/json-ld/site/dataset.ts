import type { JsonLdNode } from '../core/json-ld-node';
import { plainText } from '../core/plain-text';
import { pruneJsonLd } from '../core/prune-json-ld';
import { SCHEMA_ORG } from '../core/schema-org';

/** Data Dragon is the upstream corpus: naming it states provenance, not ownership. */
const DATA_SOURCE_URL = 'https://developer.riotgames.com/docs/lol#data-dragon';

/**
 * What the site publishes, from which source and on which terms: an answer engine can then
 * describe the project without inferring it from the data pages.
 */
export function dataset(fields: {
  readonly name: string;
  readonly url: string;
  readonly description: string;
  readonly version?: string | null;
  /** BCP 47 tags of the languages the data is published in. */
  readonly languages?: readonly string[];
  readonly keywords?: readonly string[];
  /** `@id` of the Organization node of the site graph. */
  readonly creatorId?: string;
}): JsonLdNode {
  return pruneJsonLd({
    '@context': SCHEMA_ORG,
    '@type': 'Dataset',
    '@id': `${fields.url}#dataset`,
    url: fields.url,
    name: fields.name,
    description: plainText(fields.description),
    version: fields.version,
    inLanguage: fields.languages,
    keywords: fields.keywords,
    isBasedOn: DATA_SOURCE_URL,
    creator: fields.creatorId === undefined ? undefined : { '@id': fields.creatorId },
    isAccessibleForFree: true,
  });
}
