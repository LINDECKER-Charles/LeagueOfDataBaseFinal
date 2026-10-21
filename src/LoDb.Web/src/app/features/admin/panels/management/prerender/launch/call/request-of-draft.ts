import type { PassRequest } from '../../../../../../../core/api/generated/models/pass-request';
import type { PrerenderPatchesReport } from '../../../../../../../core/api/generated/models/prerender-patches-report';
import { RESOURCE_TYPE } from '../../../../../../../core/api/generated/models/resource-type-array';
import { LOCALES } from '../../../../../../../core/i18n/locales';
import type { LaunchDraft } from '../dialog/launch-draft';

// A list covering everything it may name is no restriction: the API stores it as null.
function restricted(chosen: readonly string[], every: readonly string[]): string[] | null {
  const ordered = every.filter((item) => chosen.includes(item));
  return ordered.length === every.length ? null : ordered;
}

/**
 * The body of the launch (§ 3.6): the epoch always explicit, so that a switch while the
 * dialog is open does not move the target; the versions newest first, or null when every
 * older patch is ticked and the pass stays whole; the locales and the types in their order,
 * null when all; the priorities in their order, null when none; `skipComplete` never on a new
 * epoch, which holds nothing.
 */
export function requestOfDraft(draft: LaunchDraft, patches: PrerenderPatchesReport): PassRequest {
  const { selected, priority } = draft.selection;
  const versions = patches.patches.map((row) => row.version);
  const ticked = versions.filter((version) => selected.has(version));
  return {
    epoch: draft.epoch,
    versions: ticked.length === versions.length ? null : ticked,
    locales: restricted(draft.locales, LOCALES),
    pageTypes: restricted(draft.pageTypes, RESOURCE_TYPE),
    priority: priority.length > 0 ? [...priority] : null,
    skipComplete: draft.skipComplete && draft.epochKind !== 'new',
  };
}
