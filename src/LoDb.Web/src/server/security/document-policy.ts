// The Riot CDNs the pages hotlink: Data Dragon art, CommunityDragon previews, ability videos.
const RIOT_MEDIA = [
  'https://ddragon.leagueoflegends.com',
  'https://raw.communitydragon.org',
  'https://d28xe8vt774jo5.cloudfront.net',
].join(' ');
// Stripe's hosted pages, targets of the donation and billing forms (303 from the API).
const STRIPE_FORMS = 'https://checkout.stripe.com https://billing.stripe.com';
// Transloco's MessageFormat (@messageformat/core) compiles each ICU message with `new
// Function`: without this, no translated text survives hydration. To drop once the messages
// no longer compile at runtime (precompiled, or a runtime that interprets them).
const MESSAGE_COMPILER = "'unsafe-eval'";

/**
 * Content-Security-Policy of a document, `scriptHashes` being the sources of its inline
 * scripts. Scripts: the site's own bundles and those exact inline scripts, no other inline
 * code; `eval` stays open to the i18n message compiler alone (MESSAGE_COMPILER).
 * Styles keep 'unsafe-inline': Angular injects component styles and critical CSS inline.
 * Blobs are same-origin images (/cdn/blobs/); `data:` covers inline SVG and placeholders.
 */
export function documentPolicy(scriptHashes: readonly string[]): string {
  return [
    "default-src 'self'",
    "base-uri 'self'",
    "object-src 'none'",
    "frame-src 'none'",
    "frame-ancestors 'none'",
    `form-action 'self' ${STRIPE_FORMS}`,
    ["script-src 'self'", MESSAGE_COMPILER, ...scriptHashes].join(' '),
    "style-src 'self' 'unsafe-inline'",
    `img-src 'self' data: ${RIOT_MEDIA}`,
    `media-src 'self' ${RIOT_MEDIA}`,
    "font-src 'self'",
    "connect-src 'self'",
    "worker-src 'self'",
    "manifest-src 'self'",
  ].join('; ');
}
