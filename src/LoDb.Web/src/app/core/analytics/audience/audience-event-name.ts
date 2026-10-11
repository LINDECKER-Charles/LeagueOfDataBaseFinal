/**
 * The events the audience counts besides the page views: an outcome rendered in place of a
 * page (404, 5xx), a search started in a list, a shared build's link copied.
 */
export type AudienceEventName = 'page-error' | 'search' | 'build-share';
