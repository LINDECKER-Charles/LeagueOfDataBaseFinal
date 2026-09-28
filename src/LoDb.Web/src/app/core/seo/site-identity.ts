/**
 * The facts every page states about the site itself. The name is a brand, never translated:
 * `base.title` only titles the account pages, as it always has.
 */
export const SITE_IDENTITY = {
  name: 'League Of Data Base',
  /** Origin of the pages rendered without a request (prerender): production's. */
  productionOrigin: 'https://league-of-data-base.com',
  /** Brand mark of the Organization node (the PWA icon, L3.11). */
  logoPath: '/favicon/icon-512.png',
  /** Open Graph image of a page that has none of its own. */
  defaultImagePath: '/preview/home.png',
  /** Profiles that publicly identify the publisher: they anchor the Organization node. */
  sameAs: [
    'https://github.com/LINDECKER-Charles/LeagueOfDataBaseFinal',
    'https://github.com/LINDECKER-Charles',
  ],
  /** Search Console ownership tokens of the production domains. */
  googleSiteVerifications: [
    '7sycxfW68dJEaB5yfMSxjIm5MczFFNEWIvsoXL99z0w',
    'rtsHuRhsWtuo3cqSsG0rwrgn9Ctz4BeFeUpjqMA0NpM',
  ],
} as const;
