import type { JsonLdNode } from '../json-ld/core/json-ld-node';

/** Everything the service writes into `<head>`, computed before any DOM is touched. */
export interface HeadTags {
  readonly title: string;
  readonly metas: readonly {
    /** `name` for the standard and Twitter tags, `property` for Open Graph. */
    readonly attribute: 'name' | 'property';
    readonly key: string;
    readonly content: string;
  }[];
  readonly links: readonly {
    readonly rel: string;
    readonly href: string;
    readonly hreflang?: string;
    readonly type?: string;
    readonly title?: string;
  }[];
  /** One `<script type="application/ld+json">` per node. */
  readonly jsonLd: readonly JsonLdNode[];
}
