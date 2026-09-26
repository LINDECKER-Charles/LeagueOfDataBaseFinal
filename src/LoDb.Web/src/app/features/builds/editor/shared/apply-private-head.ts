import { PendingTasks, type Signal, effect, inject } from '@angular/core';
import { TranslocoService } from '@jsverse/transloco';
import { firstValueFrom } from 'rxjs';
import { PageDirection } from '../../../../core/layout/direction/page-direction';
import { Seo } from '../../../../core/seo/seo';

/**
 * Writes the head of a private page, "{title} · League Of Data Base", never indexed nor
 * canonical, from the translation key of its title. Called in a component's constructor: it
 * follows the title and the locale as they change. The account pages have their copy.
 */
export function applyPrivateHead(title: Signal<string>): void {
  const seo = inject(Seo);
  const transloco = inject(TranslocoService);
  const tasks = inject(PendingTasks);
  const page = inject(PageDirection);
  effect(() => {
    const key = title();
    const locale = page.locale();
    tasks.run(async () => {
      await firstValueFrom(transloco.load(locale)).catch(() => undefined);
      await seo.apply({ kind: 'private', locale, title: transloco.translate(key, {}, locale) });
    });
  });
}
