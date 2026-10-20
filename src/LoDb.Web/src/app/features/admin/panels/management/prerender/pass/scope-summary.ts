import type { PrerenderScopeView } from '../../../../../../core/api/generated/models/prerender-scope-view';
import type { AdminText } from '../../../../shared/inject-admin-text';

const SEPARATOR = ' · ';

function versionsOf(scope: PrerenderScopeView, texts: AdminText): string | null {
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
 * The perimeter of a pass in a line: its versions, its type of page, its locale and how many
 * versions go first, "14.1.1 → 14.24.1 · objets · fr"; "toutes les pages" when nothing
 * restricts it.
 */
export function scopeSummary(scope: PrerenderScopeView, texts: AdminText): string {
  const parts = [
    versionsOf(scope, texts),
    scope.pageType ? texts.term('prerender.page_types', scope.pageType) : null,
    scope.locale ?? null,
    scope.priority.length > 0
      ? texts.text('prerender.scope.priority', { count: scope.priority.length })
      : null,
  ].filter((part): part is string => part !== null);
  return parts.length > 0 ? parts.join(SEPARATOR) : texts.text('prerender.scope.all');
}
