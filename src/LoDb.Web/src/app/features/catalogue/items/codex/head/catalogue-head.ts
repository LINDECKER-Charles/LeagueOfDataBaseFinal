import { Injectable, PendingTasks, inject } from '@angular/core';
import { TranslocoService } from '@jsverse/transloco';
import { firstValueFrom } from 'rxjs';
import type { Locale } from '../../../../../core/i18n/locales';
import { Seo } from '../../../../../core/seo/seo';
import type { SeoPage } from '../../../../../core/seo/seo-page';
import type { CatalogueTexts } from './catalogue-texts';

/** The Transloco scope of the SEO texts (`public/i18n/seo/`). */
const SEO_SCOPE = 'seo';

/**
 * Writes the head of a catalogue page once the SEO texts of its locale are loaded; the
 * server render waits for it. Pages only say what their head holds.
 */
@Injectable({ providedIn: 'root' })
export class CatalogueHead {
  private readonly seo = inject(Seo);
  private readonly transloco = inject(TranslocoService);
  private readonly tasks = inject(PendingTasks);

  write(locale: Locale, build: (texts: CatalogueTexts) => SeoPage): void {
    void this.tasks.run(async () => {
      const scoped = `${SEO_SCOPE}/${locale}`;
      await firstValueFrom(this.transloco.load(scoped)).catch(() => undefined);
      const texts: CatalogueTexts = {
        seo: (key, params) => this.transloco.translate(key, params, scoped),
        main: (key) => this.transloco.translate(key, {}, locale),
      };
      await this.seo.apply(build(texts));
    });
  }
}
