/**
 * The site as Umami knows it: the id compose.umami.yaml declares (`umami.website`), and the
 * only host whose pages are measured. The same bundle serves staging, the local stacks and
 * the end-to-end runs, which stay silent because their host differs. The id is written here
 * and in the overlay, never generated: tools/umami/test checks that both agree.
 */
export const AUDIENCE_SITE = {
  websiteId: 'deee1958-0f6c-4a32-a327-aad389836c3b',
  hostname: 'league-of-data-base.com',
} as const;
