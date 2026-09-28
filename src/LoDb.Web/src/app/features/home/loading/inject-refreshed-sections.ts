import { isPlatformBrowser } from '@angular/common';
import { PLATFORM_ID, type Signal, computed, effect, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { ApiMeta } from '../../../core/api/meta/api-meta';
import type { HomeData } from '../data/home-data';
import { injectPreviewsFetcher } from './inject-previews-fetcher';
import { linkMakerOf } from './link-maker-of';
import { sectionsOf } from './sections-of';

type Sections = HomeData['sections'];

/**
 * The sections of the home, read once more in the browser when the route resolved some whose
 * images were still pending: after the delay the API asked for, and once per resolution, so
 * a cold version's placeholders fill in without a reload and without a polling loop, as the
 * catalogue lists do. The server renders what it resolved.
 */
export function injectRefreshedSections(data: Signal<HomeData>): Signal<Sections> {
  const fetchPreviews = injectPreviewsFetcher();
  const meta$ = inject(ApiMeta).meta();
  const isBrowser = isPlatformBrowser(inject(PLATFORM_ID));
  const refreshed = signal<{ readonly from: HomeData; readonly sections: Sections } | null>(null);
  effect((onCleanup) => {
    const home = data();
    const { context, retryAfterMs } = home;
    if (!isBrowser || context === null || retryAfterMs === null) {
      return;
    }
    const timer = setTimeout(async () => {
      const [meta, fetched] = await Promise.all([firstValueFrom(meta$), fetchPreviews(context)]);
      const sections = sectionsOf(fetched.previews, linkMakerOf(context, meta));
      refreshed.set({ from: home, sections });
    }, retryAfterMs);
    onCleanup(() => clearTimeout(timer));
  });
  // A later resolution (another version, another locale) drops what an earlier one read.
  return computed(() => {
    const current = refreshed();
    const home = data();
    return current?.from === home ? current.sections : home.sections;
  });
}
