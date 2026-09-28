import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { provideTranslocoScope, TranslocoPipe } from '@jsverse/transloco';
import type { PublicApiReference } from '../../core/api/generated/models/public-api-reference';
import { PageDirection } from '../../core/layout/direction/page-direction';
import { localePath } from '../../core/layout/shell/locale-path';
import { injectRouteData } from '../../core/routing/inject-route-data';
import { CANONICAL_ORIGIN } from '../../core/seo/canonical-origin';
import { Button } from '../../ui/controls/button';
import { Chip } from '../../ui/controls/chip';
import { FragmentLink } from '../../ui/navigation/fragment-link';
import { Backdrop } from '../../ui/surfaces/backdrop';
import { applyDevelopersHead } from './apply-developers-head';
import {
  DEFAULT_KEY_PREFIX,
  ERROR_SAMPLE,
  authHeaders,
  curlSamples,
  usageSample,
} from './reference/api-samples';
import { pricingRows } from './reference/pricing-rows';

/** The routes of `/v1` the page documents, with the key of their one-line purpose. */
const ENDPOINTS = [
  { path: '/healthz', purpose: 'api.developers.endpoints.healthz' },
  { path: '/v1/profiles/{username}', purpose: 'api.developers.endpoints.profiles' },
  { path: '/v1/champions/{championId}/builds', purpose: 'api.developers.endpoints.builds' },
  { path: '/v1/trends/{type}', purpose: 'api.developers.endpoints.trends' },
  { path: '/v1/usage', purpose: 'api.developers.endpoints.usage' },
] as const;

/**
 * `/{locale}/developers`: the documentation of the public API, the page being the whole
 * doc: authentication, routes, samples to copy, quotas, errors and prices. The base URL, the
 * key format and the prices come from the API's configuration (`reference`); without them
 * the page still documents the routes, against the site's origin, its prices withheld.
 */
@Component({
  selector: 'lodb-developers-page',
  imports: [Backdrop, Button, Chip, FragmentLink, RouterLink, TranslocoPipe],
  providers: [provideTranslocoScope('api', 'developers')],
  templateUrl: './developers-page.html',
  styleUrl: './developers-page.css',
  host: { class: 'relative isolate block px-6 py-12' },
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class DevelopersPage {
  protected readonly reference = injectRouteData<PublicApiReference | null>('reference');
  protected readonly locale = inject(PageDirection).locale;
  protected readonly endpoints = ENDPOINTS;
  protected readonly errorSample = ERROR_SAMPLE;
  private readonly siteOrigin = inject(CANONICAL_ORIGIN);

  protected readonly baseUrl = computed(() => this.reference()?.baseUrl ?? this.siteOrigin);
  protected readonly prefix = computed(() => this.reference()?.keyPrefix ?? DEFAULT_KEY_PREFIX);
  protected readonly headers = computed(() => authHeaders(this.prefix()));
  protected readonly curl = computed(() => curlSamples(this.baseUrl(), this.prefix()));
  protected readonly usage = computed(() => {
    const reference = this.reference();
    return reference === null ? null : usageSample(reference.freePlan);
  });
  protected readonly pricing = computed(() => {
    const reference = this.reference();
    return reference === null ? [] : pricingRows(reference);
  });

  constructor() {
    applyDevelopersHead();
  }

  protected link(path: string): string {
    return localePath(this.locale(), path);
  }
}
