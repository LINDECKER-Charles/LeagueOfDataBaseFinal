import type { PrerenderScopeView } from '../../../../../../core/api/generated/models/prerender-scope-view';
import type { AdminText } from '../../../../shared/inject-admin-text';

const SEPARATOR = ' · ';
const LIST_SEPARATOR = ', ';

// The listed versions, by their bounds: a report counts them, it does not list them.
function listedOf(scope: PrerenderScopeView, texts: AdminText): string | null {
  const { newestListed: newest, oldestListed: oldest } = scope;
  if (!newest || !oldest) {
    return null;
  }
  return newest === oldest ? newest : texts.text('prerender.scope.range', { oldest, newest });
}

// The range of a pass launched before the lists.
function rangeOf(scope: PrerenderScopeView, texts: AdminText): string | null {
  const { oldest, newest } = scope;
  if (oldest && newest) {
    return texts.text('prerender.scope.range', { oldest, newest });
  }
  if (oldest) {
    return texts.text('prerender.scope.from', { oldest });
  }
  return newest ? texts.text('prerender.scope.until', { newest }) : null;
}

/**
 * The perimeter of a pass in a line: its versions, its types of page, its locales and how
 * many versions go first, "14.1.1 → 14.24.1 · objets · fr"; "toutes les pages" when nothing
 * restricts it.
 */
export function scopeSummary(scope: PrerenderScopeView, texts: AdminText): string {
  const types = scope.pageTypes?.map((type) => texts.term('prerender.page_types', type));
  const parts = [
    listedOf(scope, texts) ?? rangeOf(scope, texts),
    types ? types.join(LIST_SEPARATOR) : null,
    scope.locales ? scope.locales.join(LIST_SEPARATOR) : null,
    scope.priority.length > 0
      ? texts.text('prerender.scope.priority', { count: scope.priority.length })
      : null,
  ].filter((part): part is string => part !== null);
  return parts.length > 0 ? parts.join(SEPARATOR) : texts.text('prerender.scope.all');
}
