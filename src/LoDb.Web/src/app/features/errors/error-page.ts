import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { provideTranslocoScope, TranslocoPipe } from '@jsverse/transloco';
import { PageDirection } from '../../core/layout/direction/page-direction';
import { localePath } from '../../core/layout/shell/locale-path';
import { injectRouteData } from '../../core/routing/inject-route-data';
import type { PageOutcome } from '../../core/routing/outcome/page-outcome';
import { Button } from '../../ui/controls/button';

/**
 * The page answered in place of the one requested (ADR 0005): a missing page, a failure, or
 * the body of a redirect the server answers with its `Location`. The status and the headers
 * (`Location`, `Retry-After`, `X-Robots-Tag: noindex`, short cache) are the `outcome`
 * resolver's; the page only says what happened and leads back home.
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
}
