import { PendingTasks, type Signal, effect, inject } from '@angular/core';
import { TranslocoService } from '@jsverse/transloco';
import { firstValueFrom } from 'rxjs';
import { PageDirection } from '../../../../core/layout/direction/page-direction';
import { Seo } from '../../../../core/seo/seo';
import type { AccountTitle } from './account-title';

/**
 * Writes the head of a private page, "{title} · League Of Data Base", never indexed nor
 * canonical, from its title: a translation key, or a text such as a summoner's name. Called
 * in a component's constructor: it follows the title and the locale as they change.
 */
export function applyPrivateHead(title: Signal<AccountTitle>): void {
  const seo = inject(Seo);
  const transloco = inject(TranslocoService);
  const tasks = inject(PendingTasks);
  const page = inject(PageDirection);
  effect(() => {
    const current = title();
    const locale = page.locale();
    tasks.run(async () => {
      await firstValueFrom(transloco.load(locale)).catch(() => undefined);
      const text = 'key' in current ? transloco.translate(current.key, {}, locale) : current.text;
      await seo.apply({ kind: 'private', locale, title: text });
    });
  });
}
