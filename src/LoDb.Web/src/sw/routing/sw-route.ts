/**
 * What the service worker does with a request: leave it to the browser, answer it network
 * first as a page, or cache first as a front asset or a storage blob.
 */
export type SwRoute = 'bypass' | 'page' | 'asset' | 'blob';
