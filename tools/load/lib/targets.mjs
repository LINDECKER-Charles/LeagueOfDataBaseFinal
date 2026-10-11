// The path each request of a scenario asks for, from the run's context (context.mjs) and the
// request's index in the scenario. Pure: the same index always gives the same path.
// Plain module: imported by the k6 scripts and by node --test alike.

import { searchTermAt, tupleAt } from './walk.mjs';

const SEARCH_LOCALE = 'en';
const WARM_UP_QUERY = 'resources=champions&resources=items&resources=runes&resources=summoners';
// An entry no patch has: the former site's answer for it is a 404 page, or the list.
const GONE_CHAMPION = 'champion/Gone';
const LEGACY_PAGE = 'faq';
// The pages a returning visitor reads, which nginx's page cache holds once read once.
const HOT_LOCALES = ['en', 'fr'];
// The probe alternates a page the cache holds and one nothing asked for before.
const PROBE_KINDS = 2;
// Cold pages a probe reads per run block, far above one every two seconds of a run.
const PROBE_BLOCK_SIZE = 1000;

// A list (the first `lists.length` picks) or one of the entries.
function pickOf(lists, entries, pick) {
  return pick < lists.length ? lists[pick] : entries[pick - lists.length];
}

/** S1: a page of an archived patch, a list or an entry, in any locale; never twice the same. */
export function pinnedPageAt(context, index) {
  const { archived, locales, lists, pages } = context;
  const sizes = [archived.length, locales.length, lists.length + pages.length];
  const [version, locale, pick] = tupleAt(sizes, index);
  return `/${locales[locale]}/${archived[version]}/${pickOf(lists, pages, pick)}`;
}

/** S2: a current list searched for something new each time. */
export function searchPageAt(context, index) {
  const list = context.lists[index % context.lists.length];
  return `/${SEARCH_LOCALE}/${list}?q=${searchTermAt(index)}`;
}

/** S3: an API read of an archived patch in any Data Dragon language, a list or an entry. */
export function catalogReadAt(context, index) {
  const { archived, languages, lists, reads } = context;
  const sizes = [archived.length, languages.length, lists.length + reads.length];
  const [version, language, pick] = tupleAt(sizes, index);
  return `/api/catalog/${archived[version]}/${languages[language]}/${pickOf(lists, reads, pick)}`;
}

// The former site's URL shapes, which engines and old links still follow.
const LEGACY_FORMS = [
  ({ version, name }) => `/${version}/${name}`,
  ({ version, language, name }) => `/${name}?version=${version}&lang=${language}`,
  ({ version, list }) => `/${version}/${list}`,
  ({ version, language, list }) => `/${list}?lang=${language}&version=${version}`,
  ({ version, round }) => `/${version}/${GONE_CHAMPION}${round}`,
  ({ language }) => `/${LEGACY_PAGE}?lang=${language}`,
];

/** S4: a URL of the former site, every shape in turn, over archived patches and languages. */
export function legacyUrlAt(context, index) {
  const { archived, languages, legacyLists, legacyNames } = context;
  const round = Math.floor(index / LEGACY_FORMS.length);
  const sizes = [archived.length, languages.length, legacyNames.length];
  const [version, language, name] = tupleAt(sizes, round);
  return LEGACY_FORMS[index % LEGACY_FORMS.length]({
    version: archived[version],
    language: languages[language],
    name: legacyNames[name],
    list: legacyLists[name % legacyLists.length],
    round,
  });
}

/** S5: the patch-switch loader's stream for an archived patch and any language. */
export function warmUpAt(context, index) {
  const { archived, languages } = context;
  const [version, language] = tupleAt([archived.length, languages.length], index);
  return `/api/catalog/${archived[version]}/${languages[language]}/warm-up?${WARM_UP_QUERY}`;
}

const TARGETS = {
  S1: pinnedPageAt,
  S2: searchPageAt,
  S3: catalogReadAt,
  S4: legacyUrlAt,
  S5: warmUpAt,
};

/** The path of the `index`-th request of a scenario (S1 to S5). */
export function targetAt(scenario, context, index) {
  const target = TARGETS[scenario];
  if (target === undefined) {
    throw new RangeError(`Unknown scenario ${scenario}: ${Object.keys(TARGETS).join(', ')}.`);
  }
  return target(context, index);
}

/**
 * The human probe's `index`-th page of the latest patch: in turn a list the cache holds and
 * an entry nothing asked for before. `block` keeps each run's cold pages apart from every
 * other run's, so that a later run never finds them cached.
 */
export function probePageAt(context, index, block) {
  const turn = Math.floor(index / PROBE_KINDS);
  if (index % PROBE_KINDS === 0) {
    const [locale, list] = tupleAt([HOT_LOCALES.length, context.lists.length], turn);
    return `/${HOT_LOCALES[locale]}/${context.lists[list]}`;
  }
  const { locales, pages } = context;
  const [locale, page] = tupleAt([locales.length, pages.length], block * PROBE_BLOCK_SIZE + turn);
  return `/${locales[locale]}/${pages[page]}`;
}
