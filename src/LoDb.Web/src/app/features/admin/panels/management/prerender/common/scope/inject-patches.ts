import { readAdminPrerenderPatches } from '../../../../../../../core/api/generated/fn/admin-prerender/read-admin-prerender-patches';
import type { PrerenderPatchesReport } from '../../../../../../../core/api/generated/models/prerender-patches-report';
import { injectPanel } from '../../../../../state/inject-panel';
import type { Panel } from '../../../../../state/panel';

/**
 * The older patches and what epoch `epoch()` holds of each (§ 6.6), read once when the page
 * or the dialog opens, again when the epoch changes and on `reload` (the end of a pass, the
 * refresh): never on a timer, it lists every older patch. Null reads the served epoch.
 */
export function injectPatches(epoch: () => number | null): Panel<PrerenderPatchesReport> {
  return injectPanel(readAdminPrerenderPatches, () => {
    const read = epoch();
    return read === null ? {} : { epoch: read };
  });
}
