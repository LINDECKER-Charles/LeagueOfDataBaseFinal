import { HttpClient } from '@angular/common/http';
import { Injectable, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { API_BASE_URL } from '../../../core/api/api-base-url';
import { createApiKey } from '../../../core/api/generated/fn/api-keys/create-api-key';
import { getApiKey } from '../../../core/api/generated/fn/api-keys/get-api-key';
import { regenerateApiKey } from '../../../core/api/generated/fn/api-keys/regenerate-api-key';
import { revokeApiKey } from '../../../core/api/generated/fn/api-keys/revoke-api-key';
import { getBillingOffers } from '../../../core/api/generated/fn/billing/get-billing-offers';
import { openPackCheckout } from '../../../core/api/generated/fn/billing/open-pack-checkout';
import { openPlanCheckout } from '../../../core/api/generated/fn/billing/open-plan-checkout';
import type { ApiKeyOverview } from '../../../core/api/generated/models/api-key-overview';
import type { BillingOffers } from '../../../core/api/generated/models/billing-offers';
import type { IssuedApiKey } from '../../../core/api/generated/models/issued-api-key';
import { PORTAL_PAYMENTS } from '../shared/portal-payments';
import { portalFailure } from './portal-failure';
import type { PortalNotice } from './portal-notice';
import { PortalStripeRedirect } from './stripe-redirect';

/** A purchase of the portal: a credit pack or a plan, by its code. */
export interface PortalPurchase {
  readonly kind: 'pack' | 'plan';
  readonly code: string;
  /** The locale Stripe's page speaks and comes back to. */
  readonly locale: string;
}

type LoadStatus = 'loading' | 'ready' | 'failed';

/**
 * The state of the API portal and its actions: the account's key, the offers on sale, the
 * secret just issued, and the banner of the last outcome. The secret lives in this service
 * alone, the time the page shows it: nothing stores it, and a reload forgets it for good.
 * Provided by the page, so that leaving it drops the secret.
 */
@Injectable()
export class ApiKeyPortal {
  private readonly http = inject(HttpClient);
  private readonly apiOrigin = inject(API_BASE_URL);
  private readonly redirect = inject(PortalStripeRedirect);
  private readonly payments = inject(PORTAL_PAYMENTS);

  readonly status = signal<LoadStatus>('loading');
  readonly key = signal<ApiKeyOverview | null>(null);
  readonly secret = signal<string | null>(null);
  readonly offers = signal<BillingOffers | null>(null);
  readonly notice = signal<PortalNotice | null>(null);
  readonly busy = signal(false);

  /** Reads the key and, where the build sells, the offers; a failed read can be retried. */
  async load(): Promise<void> {
    this.status.set('loading');
    const offers = this.payments ? this.loadOffers() : Promise.resolve();
    try {
      const state = await firstValueFrom(getApiKey(this.http, this.apiOrigin));
      this.key.set(state.body.key);
      this.status.set('ready');
    } catch {
      this.status.set('failed');
    }
    await offers;
  }

  /** Shows a banner, such as the one of a return from Stripe. */
  announce(notice: PortalNotice | null): void {
    this.notice.set(notice);
  }

  create(name: string): Promise<void> {
    return this.run(async () => {
      const body = { name };
      const issued = await firstValueFrom(createApiKey(this.http, this.apiOrigin, { body }));
      this.reveal(issued.body, 'api.portal.flash.created');
    });
  }

  regenerate(): Promise<void> {
    return this.run(async () => {
      const issued = await firstValueFrom(regenerateApiKey(this.http, this.apiOrigin));
      this.reveal(issued.body, 'api.portal.flash.regenerated');
    });
  }

  revoke(): Promise<void> {
    return this.run(async () => {
      await firstValueFrom(revokeApiKey(this.http, this.apiOrigin));
      this.key.set(null);
      this.secret.set(null);
      this.notice.set({ tone: 'success', key: 'api.portal.flash.revoked' });
    });
  }

  /** Opens Stripe's page for a pack or a plan, and leaves for it. */
  buy(purchase: PortalPurchase): Promise<void> {
    return this.run(async () => {
      const created = await firstValueFrom(this.checkout(purchase));
      if (!this.redirect.go(created.body.url)) {
        this.notice.set({ tone: 'error', key: 'api.portal.flash.gateway' });
      }
    });
  }

  /** Forgets the secret once its owner copied it. */
  dismissSecret(): void {
    this.secret.set(null);
  }

  private checkout({ kind, code, locale }: PortalPurchase) {
    return kind === 'pack'
      ? openPackCheckout(this.http, this.apiOrigin, { body: { pack: code, locale } })
      : openPlanCheckout(this.http, this.apiOrigin, { body: { plan: code, locale } });
  }

  // Without an answer, nothing is on sale: the panel says payments are unavailable.
  private async loadOffers(): Promise<void> {
    try {
      this.offers.set((await firstValueFrom(getBillingOffers(this.http, this.apiOrigin))).body);
    } catch {
      this.offers.set(null);
    }
  }

  private reveal(issued: IssuedApiKey, message: string): void {
    this.key.set(issued.key);
    this.secret.set(issued.secret);
    this.notice.set({ tone: 'success', key: message });
  }

  // One action at a time; its refusal becomes the banner.
  private async run(action: () => Promise<void>): Promise<void> {
    if (this.busy()) {
      return;
    }
    this.busy.set(true);
    this.notice.set(null);
    try {
      await action();
    } catch (error) {
      this.notice.set({ tone: 'error', key: portalFailure(error) });
    } finally {
      this.busy.set(false);
    }
  }
}
