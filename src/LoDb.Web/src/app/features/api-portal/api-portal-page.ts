import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { provideTranslocoScope, TranslocoPipe } from '@jsverse/transloco';
import { AuthSession } from '../../core/auth/session/auth-session';
import { PageDirection } from '../../core/layout/direction/page-direction';
import { localePath } from '../../core/layout/shell/locale-path';
import { Button } from '../../ui/controls/button';
import { Backdrop } from '../../ui/surfaces/backdrop';
import { OffersPanel } from './billing/offers-panel';
import { CreateKey } from './key/create-key';
import { KeyOverview } from './key/key-overview';
import { KeyReveal } from './key/key-reveal';
import { UsageTable } from './key/usage-table';
import { API_PORTAL_SCOPE, API_SCOPE } from './shared/api-portal-scope';
import { applyPortalHead } from './shared/apply-portal-head';
import { PORTAL_PAYMENTS } from './shared/portal-payments';
import { type PortalPurchase, ApiKeyPortal } from './state/api-key-portal';
import { checkoutNotice } from './state/checkout-status';

/**
 * `/{locale}/account/api`: the account's API key, its one-time secret, its usage, and the
 * packs and plans that raise it. The secret shows once, right after it is issued; the offers
 * show only where the build sells (`PORTAL_PAYMENTS`), the key everywhere. Rendered in the
 * browser only, behind the sign-in.
 */
@Component({
  selector: 'lodb-api-portal-page',
  imports: [
    Backdrop,
    Button,
    CreateKey,
    KeyOverview,
    KeyReveal,
    OffersPanel,
    RouterLink,
    TranslocoPipe,
    UsageTable,
  ],
  providers: [provideTranslocoScope(API_SCOPE, API_PORTAL_SCOPE), ApiKeyPortal],
  templateUrl: './api-portal-page.html',
  styleUrls: ['./shared/portal.css', './api-portal-page.css'],
  host: { class: 'relative isolate block flex-1' },
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ApiPortalPage {
  protected readonly portal = inject(ApiKeyPortal);
  protected readonly locale = inject(PageDirection).locale;
  protected readonly payments = inject(PORTAL_PAYMENTS);
  protected readonly verified = inject(AuthSession).isEmailVerified;

  constructor() {
    applyPortalHead();
    const status = inject(ActivatedRoute).snapshot.queryParamMap.get('status');
    this.portal.announce(checkoutNotice(status));
    void this.portal.load();
  }

  protected link(path: string): string {
    return localePath(this.locale(), path);
  }

  protected buy(choice: Omit<PortalPurchase, 'locale'>): void {
    void this.portal.buy({ ...choice, locale: this.locale() });
  }
}
