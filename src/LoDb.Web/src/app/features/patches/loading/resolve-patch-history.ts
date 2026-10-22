import { inject } from '@angular/core';
import type { ResolveFn } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { ApiMeta } from '../../../core/api/meta/api-meta';
import { DEFAULT_LOCALE } from '../../../core/i18n/default-locale';
import { isLocale } from '../../../core/i18n/is-locale';
import type { PatchHistory } from '../data/patch-history';

/**
 * Resolver of the patch history (`history`): every version `/api/meta` lists, which the
 * render requests once and shares, and nothing else, so the page costs no catalogue read
 * however many versions it links. `/api/meta` out of reach fails the navigation (502 or
 * 503), as on the home page. The page keeps the cache of the latest version: it changes
 * when a patch is promoted.
 */
export const resolvePatchHistory: ResolveFn<PatchHistory> = async (route) => {
  const meta$ = inject(ApiMeta).meta();
  const requested = route.paramMap.get('locale');
  const locale = isLocale(requested) ? requested : DEFAULT_LOCALE;
  const meta = await firstValueFrom(meta$);
  return {
    locale,
    latest: meta.latest ?? null,
    versions: meta.versions,
    oldestChangesVersion: meta.oldestChangesVersion,
    newestChangesVersion: meta.newestChangesVersion,
  };
};
