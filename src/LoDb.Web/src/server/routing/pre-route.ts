/**
 * The answer the pre-router settles on before any render (`preRouteOf`): a refusal, the
 * static 404, a redirect to the cleaned URL (root-relative), or the render of the page.
 */
export type PreRoute =
  | { readonly kind: 'bad-request' }
  | { readonly kind: 'not-found' }
  | { readonly kind: 'redirect'; readonly location: string }
  | { readonly kind: 'render' };
