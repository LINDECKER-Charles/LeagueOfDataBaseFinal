import type { NormalizedRedirectReason } from '../metrics/normalized-redirect-reason';

/**
 * The answer the pre-router settles on before any render (`preRouteOf`): a refusal, the
 * static 404, a redirect to the page's one URL (root-relative) and why, or the render of the
 * page.
 */
export type PreRoute =
  | { readonly kind: 'bad-request' }
  | { readonly kind: 'not-found' }
  | {
      readonly kind: 'redirect';
      readonly location: string;
      readonly reason: Exclude<NormalizedRedirectReason, 'canonical'>;
    }
  | { readonly kind: 'render' };
