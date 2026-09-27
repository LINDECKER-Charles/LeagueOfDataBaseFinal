import {
  ChangeDetectionStrategy,
  Component,
  PendingTasks,
  computed,
  effect,
  inject,
} from '@angular/core';
import { RouterLink } from '@angular/router';
import { provideTranslocoScope, TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import { firstValueFrom } from 'rxjs';
import type { ResourceType } from '../../core/api/generated/models/resource-type';
import type { Locale } from '../../core/i18n/locales';
import { PageDirection } from '../../core/layout/direction/page-direction';
import { injectRouteData } from '../../core/routing/inject-route-data';
import { Seo } from '../../core/seo/seo';
import { Chip } from '../../ui/controls/chip';
import { Logo } from '../../ui/media/logo';
import { Backdrop } from '../../ui/surfaces/backdrop';
import { Frame } from '../../ui/surfaces/frame';
import type { HomeData } from './data/home-data';
import { injectRefreshedSections } from './loading/inject-refreshed-sections';
import type { CardLook } from './sections/card-look';
import { PreviewSection } from './sections/preview-section';
import { RESOURCE_TEXTS } from './sections/resource-texts';
import { SeeAllArrow } from './sections/see-all-arrow';

/** The Transloco scope of the home's own texts (`public/i18n/home/`). */
const HOME_SCOPE = 'home';
const SEO_SCOPE = 'seo';

// The portals follow the header's codex; the previews keep the legacy home's order.
const PORTAL_ORDER: readonly ResourceType[] = ['champions', 'items', 'runes', 'summoners'];
const PREVIEWS: readonly { readonly resource: ResourceType; readonly look: CardLook }[] = [
  { resource: 'champions', look: 'portrait' },
  { resource: 'items', look: 'tile' },
  { resource: 'summoners', look: 'tile' },
  { resource: 'runes', look: 'round' },
];

/**
 * The home, `/{locale}/`: a hero naming the version and language it reads, the size of each
 * list, four portals into the catalogue and a preview of each resource, all resolved by the
 * route (`resolveHome`). Its head is the site's own: canonical `/{locale}/` whatever the
 * query, 21 alternates and `x-default`, the SEO title and description of the legacy home.
 */
@Component({
  selector: 'lodb-home-page',
  imports: [Backdrop, Chip, Frame, Logo, PreviewSection, RouterLink, SeeAllArrow, TranslocoPipe],
  providers: [provideTranslocoScope(HOME_SCOPE)],
  templateUrl: './home-page.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class HomePage {
  protected readonly data = injectRouteData<HomeData>('home');
  private readonly sections = injectRefreshedSections(this.data);
  protected readonly texts = RESOURCE_TEXTS;
  protected readonly version = computed(() => this.data().context?.version ?? '');
  protected readonly portals = computed(() => {
    const sections = this.sections();
    return PORTAL_ORDER.map((resource) => sections[resource]);
  });
  protected readonly previews = computed(() => {
    const sections = this.sections();
    return PREVIEWS.map(({ resource, look }) => ({ section: sections[resource], look }));
  });

  private readonly seo = inject(Seo);
  private readonly transloco = inject(TranslocoService);

  constructor() {
    const tasks = inject(PendingTasks);
    const page = inject(PageDirection);
    // The router reuses the page from one locale's home to another's, and on `?version=`.
    effect(() => {
      const locale = page.locale();
      const version = this.data().context?.version ?? null;
      tasks.run(() => this.writeHead(locale, version));
    });
  }

  private async writeHead(locale: Locale, version: string | null): Promise<void> {
    const scoped = `${SEO_SCOPE}/${locale}`;
    await firstValueFrom(this.transloco.load(scoped)).catch(() => undefined);
    // The SEO title is the whole document title: no site name appended.
    const title = this.transloco.translate('home.title', {}, scoped);
    const description =
      version === null
        ? {}
        : { description: this.transloco.translate('home.description', { version }, scoped) };
    await this.seo.apply({ title, path: '', titleFormat: 'raw', locale, ...description });
  }
}
