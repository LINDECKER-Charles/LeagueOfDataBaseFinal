/**
 * Classes of response of ADR 0005, each with its own `Cache-Control`: the latest version,
 * an older version pinned in the URL whose page never changes again (a closed state) or may
 * change once (the open state, until a promotion closes it), redirects and errors, and
 * private pages.
 */
export type CacheClass = 'latest' | 'archived' | 'revisable' | 'transient' | 'private';
