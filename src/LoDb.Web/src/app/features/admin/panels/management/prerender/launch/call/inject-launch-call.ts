import { HttpClient } from '@angular/common/http';
import { type Signal, inject, signal } from '@angular/core';
import { firstValueFrom, map, timeout } from 'rxjs';
import { API_BASE_URL } from '../../../../../../../core/api/api-base-url';
import { readAdminPrerender } from '../../../../../../../core/api/generated/fn/admin-prerender/read-admin-prerender';
import { startAdminPrerenderPass } from '../../../../../../../core/api/generated/fn/admin-prerender/start-admin-prerender-pass';
import type { PassRequest } from '../../../../../../../core/api/generated/models/pass-request';
import type { PrerenderPassStarted } from '../../../../../../../core/api/generated/models/prerender-pass-started';
import type { PrerenderReport } from '../../../../../../../core/api/generated/models/prerender-report';
import { ADMIN_TIMEOUT } from '../../../../../shared/http/admin-timeout';
import { type AdminProblem, problemOf } from '../../../../../shared/http/problem-of';

/** How a launch went: the pass it opened, or the problem that refused it. */
export type LaunchOutcome =
  | { readonly started: PrerenderPassStarted; readonly problem?: undefined }
  | { readonly started?: undefined; readonly problem: AdminProblem };

/** The calls of the launch dialog. */
export interface LaunchCall {
  /** Whether a call is in flight: the launch button is busy, never clicked twice. */
  readonly isBusy: Signal<boolean>;
  /** Opens the pass. */
  start(request: PassRequest): Promise<LaunchOutcome>;
  /** Reads the report again, for « Vérifier »; null when it could not. */
  check(): Promise<PrerenderReport | null>;
}

// What the calls need, injected once.
interface Api {
  readonly http: HttpClient;
  readonly rootUrl: string;
  readonly wait: number;
}

function startPass(api: Api, request: PassRequest): Promise<LaunchOutcome> {
  return firstValueFrom(
    startAdminPrerenderPass(api.http, api.rootUrl, { body: request }).pipe(
      timeout({ first: api.wait }),
      map((response): LaunchOutcome => ({ started: response.body })),
    ),
  ).catch((error: unknown): LaunchOutcome => ({ problem: problemOf(error) }));
}

function readReport(api: Api): Promise<PrerenderReport | null> {
  return firstValueFrom(
    readAdminPrerender(api.http, api.rootUrl).pipe(
      timeout({ first: api.wait }),
      map((response) => response.body),
    ),
  ).catch(() => null);
}

/**
 * The calls of the launch dialog, through the generated client and within `ADMIN_TIMEOUT`,
 * as `AdminCommand` runs them; but a refusal is kept and handed back, for the dialog to show
 * it in place rather than in the band of the page (§ 3.6).
 */
export function injectLaunchCall(): LaunchCall {
  const api: Api = {
    http: inject(HttpClient),
    rootUrl: inject(API_BASE_URL),
    wait: inject(ADMIN_TIMEOUT),
  };
  const isBusy = signal(false);
  const run = async <T>(call: () => Promise<T>): Promise<T> => {
    isBusy.set(true);
    try {
      return await call();
    } finally {
      isBusy.set(false);
    }
  };
  return {
    isBusy: isBusy.asReadonly(),
    start: (request) => run(() => startPass(api, request)),
    check: () => run(() => readReport(api)),
  };
}
