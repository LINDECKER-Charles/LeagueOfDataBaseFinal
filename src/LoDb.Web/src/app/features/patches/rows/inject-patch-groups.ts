import { type Signal, computed, inject } from '@angular/core';
import { Router } from '@angular/router';
import { RESOURCE_TYPE } from '../../../core/api/generated/models/resource-type-array';
import { hasChangesPage } from '../../../core/api/meta/has-changes-page';
import { PATCH_CHANGES_PATH } from '../../../core/routing/patch-changes-path';
import { cataloguePath } from '../../../core/routing/url/catalogue-path';
import type { PatchGroup } from '../data/patch-group';
import type { PatchHistory } from '../data/patch-history';
import type { PatchRow } from '../data/patch-row';
import { versionGroupsOf } from './version-groups-of';

/**
 * The rows of the history, grouped by first segment, each linking the page of its version's
 * changes where it has one, then its four lists: under the short URLs for the latest one,
 * pinned for any other (ADR 0005), so no link answers a redirect. UrlTrees, as every
 * catalogue link; no regional variant is carried: the history has one address per locale.
 */
export function injectPatchGroups(history: () => PatchHistory): Signal<readonly PatchGroup[]> {
  const router = inject(Router);
  return computed(() => {
    const current = history();
    const { locale, latest, versions } = current;
    const urlOf = (path: string, version: string | null) =>
      router.parseUrl(cataloguePath(locale, path, { version, lang: null }));
    const rowOf = (version: string): PatchRow => {
      const pinned = version === latest ? null : version;
      return {
        version,
        isLatest: pinned === null,
        changes: hasChangesPage(version, current) ? urlOf(PATCH_CHANGES_PATH, pinned) : null,
        lists: RESOURCE_TYPE.map((resource) => ({ resource, url: urlOf(resource, pinned) })),
      };
    };
    return versionGroupsOf(versions).map((group) => ({
      major: group.major,
      rows: group.versions.map(rowOf),
    }));
  });
}
