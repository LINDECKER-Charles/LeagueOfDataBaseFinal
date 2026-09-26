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
import { PageDirection } from '../../core/layout/direction/page-direction';
import { localePath } from '../../core/layout/shell/locale-path';
import { injectRouteData } from '../../core/routing/inject-route-data';
import type { PageOutcome } from '../../core/routing/outcome/page-outcome';
import { Seo } from '../../core/seo/seo';
import { Button } from '../../ui/controls/button';

/**
 * The page answered in place of the one requested (ADR 0005): a missing page, a failure, or
 * the body of a redirect the server answers with its `Location`. The status and the headers
 * (`Location`, `Retry-After`, `X-Robots-Tag: noindex`, short cache) are the `outcome`
 * resolver's; the page only says what happened, leads back home, and writes a `noindex`
 * head without canonical nor alternates.
 */
@Component({
  selector: 'lodb-error-page',
  imports: [Button, RouterLink, TranslocoPipe],
  providers: [provideTranslocoScope('seo')],
  templateUrl: './error-page.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ErrorPage {
  protected readonly outcome = injectRouteData<PageOutcome>('outcome');
  private readonly page = inject(PageDirection);
  protected readonly home = computed(() => localePath(this.page.locale(), ''));

  constructor() {
    const seo = inject(Seo);
    const transloco = inject(TranslocoService);
    const tasks = inject(PendingTasks);
    // The router reuses the page from one outcome to the next: its head follows the outcome.
    effect(() => {
      const locale = this.page.locale();
      const key = this.outcome().kind === 'not-found' ? 'error.404.title' : 'error.generic.title';
      tasks.run(async () => {
        const scope = `seo/${locale}`;
        await firstValueFrom(transloco.load(scope)).catch(() => undefined);
        await seo.apply({ kind: 'error', locale, title: transloco.translate(key, {}, scope) });
      });
    });
  }
}
