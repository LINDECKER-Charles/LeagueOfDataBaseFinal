// The production URLs the diff compares, in the legacy grammar: each is asked of production
// as is, and of the stack, whose 301 (L3.12) names the equivalent page. Chosen to cover every
// kind of indexable page: home, lists, details of each resource with their LoL Classic twins
// and awkward ids, pages of past patches, regional languages and editorial pages.
//
// Left out on purpose: /trends, /developers and /donate, still provisional pages of lots 5
// and 6; /u/{username}, whose accounts only exist in production's database (lot 8); the
// account pages, never indexed.

// A patch of the current season and one of the previous: the prerendered sitemaps of
// production keep both.
const RECENT_PATCH = '16.14.1';
const PREVIOUS_SEASON_PATCH = '15.24.1';

/** `{ group, url }` of each production page, `url` relative to production's origin. */
export const SAMPLE = [
  { group: 'home', url: '/' },
  { group: 'list', url: '/champions' },
  { group: 'list', url: '/objects' },
  { group: 'list', url: '/runes' },
  { group: 'list', url: '/summoners' },
  { group: 'champion', url: '/champion/Annie' },
  { group: 'champion', url: '/champion/MonkeyKing' },
  { group: 'champion', url: '/champion/Nunu' },
  { group: 'champion', url: '/champion/KSante' },
  { group: 'champion', url: '/champion/Belveth' },
  { group: 'item', url: '/object/1001' },
  { group: 'item', url: '/object/3031' },
  { group: 'item', url: '/object/2003' },
  { group: 'item', url: '/object/3340' },
  { group: 'item', url: '/object/1004' },
  { group: 'classic twin', url: '/object/771004' },
  { group: 'rune path', url: '/rune/Domination' },
  { group: 'rune path', url: '/rune/Precision' },
  { group: 'rune path', url: '/rune/Inspiration' },
  { group: 'summoner spell', url: '/summoner/SummonerFlash' },
  { group: 'classic twin', url: '/summoner/SummonerFlash_Jade' },
  { group: 'summoner spell', url: '/summoner/SummonerSmite' },
  { group: 'summoner spell', url: '/summoner/SummonerSnowball' },
  { group: 'past patch', url: `/${RECENT_PATCH}/champions` },
  { group: 'past patch', url: `/${RECENT_PATCH}/objects` },
  { group: 'past patch', url: `/${RECENT_PATCH}/champion/Aatrox` },
  { group: 'past patch', url: `/${RECENT_PATCH}/object/3031` },
  { group: 'past patch', url: `/${RECENT_PATCH}/rune/Domination` },
  { group: 'past patch', url: `/${RECENT_PATCH}/summoner/SummonerFlash` },
  { group: 'past patch', url: `/${PREVIOUS_SEASON_PATCH}/champion/Annie` },
  { group: 'language', url: '/champions?lang=fr_FR' },
  { group: 'language', url: '/champion/Annie?lang=fr_FR' },
  { group: 'language', url: '/object/3031?lang=ko_KR' },
  { group: 'language', url: '/champion/Ahri?lang=en_GB' },
  { group: 'editorial', url: '/about' },
  { group: 'editorial', url: '/about/data' },
  { group: 'editorial', url: '/faq' },
  { group: 'editorial', url: '/changelog' },
  { group: 'editorial', url: '/legal/notice' },
  { group: 'editorial', url: '/legal/privacy' },
  { group: 'editorial', url: '/legal/terms' },
  { group: 'editorial', url: '/legal/cookies' },
];

/**
 * The URL the stack is asked for a production URL. `/` has no legacy redirect (the stack
 * answers it by `Accept-Language`, ADR 0005): its legacy name `/home` stands for it.
 */
export function legacyUrlOf(url) {
  return url === '/' || url.startsWith('/?') ? `/home${url.slice(1)}` : url;
}
