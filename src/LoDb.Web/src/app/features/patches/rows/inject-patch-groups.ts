import { type Signal, computed, inject } from '@angular/core';
import { Router } from '@angular/router';
import { RESOURCE_TYPE } from '../../../core/api/generated/models/resource-type-array';
import { cataloguePath } from '../../../core/routing/url/catalogue-path';
import type { PatchGroup } from '../data/patch-group';
import type { PatchHistory } from '../data/patch-history';
import type { PatchRow } from '../data/patch-row';
import { versionGroupsOf } from './version-groups-of';

/**
 * The rows of the history, grouped by first segment, each linking the four lists of its
 * version: under the short URLs for the latest one, pinned for any other (ADR 0005), so no
 * link answers a redirect. UrlTrees, as every catalogue link; no regional variant is carried:
 * the history has one address per locale.
 */
export function injectPatchGroups(history: () => PatchHistory): Signal<readonly PatchGroup[]> {
  const router = inject(Router);
  return computed(() => {
    const { locale, latest, versions } = history();
    const rowOf = (version: string): PatchRow => {
      const pinned = version === latest ? null : version;
      return {
        version,
        isLatest: pinned === null,
        lists: RESOURCE_TYPE.map((resource) => ({
          resource,
          url: router.parseUrl(cataloguePath(locale, resource, { version: pinned, lang: null })),
        })),
      };
    };
    return versionGroupsOf(versions).map((group) => ({
      major: group.major,
      rows: group.versions.map(rowOf),
    }));
  });
}
