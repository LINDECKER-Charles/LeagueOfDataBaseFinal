// The Riot CDNs the pages hotlink: Data Dragon art, CommunityDragon previews, ability videos.
const RIOT_MEDIA = [
  'https://ddragon.leagueoflegends.com',
  'https://raw.communitydragon.org',
  'https://d28xe8vt774jo5.cloudfront.net',
].join(' ');
// Stripe's hosted pages, targets of the donation and billing forms (303 from the API).
const STRIPE_FORMS = 'https://checkout.stripe.com https://billing.stripe.com';

/**
 * Content-Security-Policy of a document, `scriptHashes` being the sources of its inline
 * scripts. Scripts: the site's own bundles and those exact inline scripts, no other inline
 * code, and no `eval` nor `new Function`: the i18n messages are compiled at build time
 * (src/LoDb.Web/i18n), never in the page.
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
    ["script-src 'self'", ...scriptHashes].join(' '),
    "style-src 'self' 'unsafe-inline'",
    `img-src 'self' data: ${RIOT_MEDIA}`,
    `media-src 'self' ${RIOT_MEDIA}`,
    "font-src 'self'",
    "connect-src 'self'",
    "worker-src 'self'",
    "manifest-src 'self'",
  ].join('; ');
}
