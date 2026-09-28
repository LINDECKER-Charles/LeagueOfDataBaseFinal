import { inject } from '@angular/core';
import type { CanMatchFn } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { ApiMeta } from '../../api/meta/api-meta';

/**
 * Lets `:version` match a segment shaped like a version, by the pattern of `/api/meta`,
 * never a copy of it. Whether Data Dragon lists that version is the canonical rule's call:
 * `/en/99.99.1/champions` then answers a real 404 with a short cache, the version may be
 * published tomorrow. An API that cannot answer fails the navigation: a 503, not a 404.
 */
export const canMatchVersion: CanMatchFn = async (_route, segments) => {
  const meta = inject(ApiMeta);
  const segment = segments[0]?.path;
  return segment !== undefined && (await firstValueFrom(meta.versionMatcher())).test(segment);
};
