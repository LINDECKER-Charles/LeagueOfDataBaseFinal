import type { PrerenderScopeView } from '../../../../../../../core/api/generated/models/prerender-scope-view';
import type { AdminText } from '../../../../../shared/inject-admin-text';
import { OPENERS_PAGES } from '../patches/openers-pages';

const SCOPE = 'prerender.common.scope';
const SEPARATOR = ' · ';
const LIST_SEPARATOR = ', ';
// A few locales read better by their codes than by their count: « en, fr, zh-hans ».
const NAMED_LOCALES = 3;

// The versions a pass lists, by their count and bounds: list views do not carry the list.
function listedOf(scope: PrerenderScopeView, texts: AdminText): string | null {
  const { versionCount: count, newestListed: newest, oldestListed: oldest } = scope;
  if (count === null || count === undefined) {
    return null;
  }
  if (count === 1 && newest) {
    return newest;
  }
  return texts.text(`${SCOPE}.${newest && oldest ? 'listed' : 'versions'}`, {
    count,
    newest,
    oldest,
  });
}

// The range of a pass launched before the lists, open at either end.
function rangeOf(scope: PrerenderScopeView, texts: AdminText): string | null {
  const { oldest, newest } = scope;
  if (oldest && newest) {
    return oldest === newest ? oldest : texts.text(`${SCOPE}.range`, { oldest, newest });
  }
  if (oldest) {
    return texts.text(`${SCOPE}.from`, { oldest });
  }
  return newest ? texts.text(`${SCOPE}.until`, { newest }) : null;
}

function localesOf(locales: readonly string[], texts: AdminText): string {
  return locales.length <= NAMED_LOCALES
    ? locales.join(LIST_SEPARATOR)
    : texts.text(`${SCOPE}.locales`, { count: locales.length });
}

// What restricts the pass beyond its versions: its locales and its types of page, both
// spelled out once anything restricts it, so that a line never reads ambiguous.
function perimeterOf(scope: PrerenderScopeView, texts: AdminText, versions: string | null) {
  const { locales, pageTypes } = scope;
  if (versions === null && !locales && !pageTypes) {
    return [texts.text(`${SCOPE}.all`)];
  }
  return [
    versions ?? texts.text(`${SCOPE}.all`),
    locales ? localesOf(locales, texts) : texts.text(`${SCOPE}.locales_all`),
    pageTypes
      ? pageTypes.map((type) => texts.term('prerender.page_types', type)).join(LIST_SEPARATOR)
      : texts.text(`${SCOPE}.types_all`),
  ];
}

/**
 * The perimeter of a pass in a line, from the summarised scope of a list view (§ 6.3):
 * « 212 patchs (16.19.1 → 14.1.1) · 21 langues · tous types · 3 prioritaires », « 16.19.1 ·
 * fr · objets », « tous les anciens patchs » when nothing restricts it, « ouvreurs d'abord »
 * when only the patch page and the openers of the index are rendered, and « manques
 * seulement » when the units already held were left out.
 */
export function scopeSummary(scope: PrerenderScopeView, texts: AdminText): string {
  const versions = listedOf(scope, texts) ?? rangeOf(scope, texts);
  const parts = perimeterOf(scope, texts, versions);
  if (scope.pages === OPENERS_PAGES) {
    parts.push(texts.text(`${SCOPE}.openers`));
  }
  if (scope.priority.length > 0) {
    parts.push(texts.text(`${SCOPE}.priority`, { count: scope.priority.length }));
  }
  if (scope.skipComplete) {
    parts.push(texts.text(`${SCOPE}.skip`));
  }
  return parts.join(SEPARATOR);
}
