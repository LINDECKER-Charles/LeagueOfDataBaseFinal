import { Injectable, PendingTasks, inject } from '@angular/core';
import { TranslocoService } from '@jsverse/transloco';
import { firstValueFrom } from 'rxjs';
import { DEFAULT_LOCALE } from '../i18n/default-locale';
import { isLocale } from '../i18n/is-locale';
import type { Locale } from '../i18n/locales';
import { CANONICAL_ORIGIN } from './canonical-origin';
import { buildHeadTags } from './head/build-head-tags';
import { HeadWriter } from './head/head-writer';
import type { SeoTexts } from './head/seo-texts';
import type { SeoPage } from './seo-page';

/** The Transloco scope of the SEO texts (`public/i18n/seo/`). */
const SEO_SCOPE = 'seo';
/** "(patch {{ version }})", after the title of a pinned page. */
const VERSIONED_SUFFIX = 'versioned_suffix';
/** "(patches {{ first }} to {{ last }})", after the title of a closed state's opener. */
const RANGE_SUFFIX = 'state_range_suffix';

/**
 * The head of every page: title, description, robots, canonical, `hreflang` alternates,
 * Open Graph, Twitter and JSON-LD. Pages only pass their data ({@link SeoPage}); the rules
 * live here, so no page can drift from them.
 */
@Injectable({ providedIn: 'root' })
export class Seo {
  private readonly transloco = inject(TranslocoService);
  private readonly pendingTasks = inject(PendingTasks);
  private readonly writer = inject(HeadWriter);
  private readonly origin = inject(CANONICAL_ORIGIN);
  private latest = 0;

  /**
   * Writes the head of `page`, once the texts it needs are loaded. The SSR render waits for
   * it; when pages follow each other faster, only the last one's head is written.
   */
  apply(page: SeoPage): Promise<void> {
    const done = this.pendingTasks.add();
    this.latest += 1;
    return this.write(page, this.latest).finally(done);
  }

  private async write(page: SeoPage, call: number): Promise<void> {
    const locale = page.locale ?? this.activeLocale();
    const texts = await this.textsOf(locale, page);
    if (call === this.latest) {
      this.writer.write(buildHeadTags({ page, texts, origin: this.origin, locale }));
    }
  }

  private activeLocale(): Locale {
    const active = this.transloco.getActiveLang();
    return isLocale(active) ? active : DEFAULT_LOCALE;
  }

  private async textsOf(locale: Locale, page: SeoPage): Promise<SeoTexts> {
    const scoped = `${SEO_SCOPE}/${locale}`;
    await Promise.all([this.load(locale), this.load(scoped)]);
    return {
      siteTitle: this.transloco.translate('base.title', {}, locale),
      siteDescription: this.transloco.translate('base.description', {}, locale),
      versionedSuffix: this.patchSuffixOf(page, scoped),
    };
  }

  // A pinned page names its patch after its title, or the run of patches of the state it
  // opens: one suffix, never two.
  private patchSuffixOf(page: SeoPage, scoped: string): string | null {
    const version = 'version' in page ? (page.version ?? null) : null;
    const patches = 'titlePatches' in page ? page.titlePatches : undefined;
    if (version === null || patches === false) {
      return null;
    }
    if (patches === undefined || patches.first === patches.last) {
      const named = patches?.first ?? version;
      return this.transloco.translate(VERSIONED_SUFFIX, { version: named }, scoped);
    }
    return this.transloco.translate(RANGE_SUFFIX, { ...patches }, scoped);
  }

  // A catalogue that fails to load must not leave the page without a head: keys then fall
  // back to `en`, like everywhere else.
  private load(path: string): Promise<unknown> {
    return firstValueFrom(this.transloco.load(path)).catch(() => undefined);
  }
}
