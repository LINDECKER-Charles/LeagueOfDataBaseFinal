/**
 * Classes of response of ADR 0005, each with its own `Cache-Control`: the latest version,
 * an older version pinned in the URL, redirects and errors, and private pages.
 */
export type CacheClass = 'latest' | 'archived' | 'transient' | 'private';
