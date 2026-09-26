import { HttpErrorResponse, HttpHeaders, HttpStatusCode } from '@angular/common/http';
import { provideLocationMocks } from '@angular/common/testing';
import { Component, ErrorHandler, PLATFORM_ID, RESPONSE_INIT } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import {
  type ActivatedRouteSnapshot,
  provideRouter,
  type ResolveFn,
  Router,
  type Routes,
  withNavigationErrorHandler,
  withRouterConfig,
} from '@angular/router';
import { throwError } from 'rxjs';
import { handleNavigationError } from '../failure/handle-navigation-error';
import { injectOutcomeCommand } from './inject-outcome-command';
import { isOutcomeNavigation } from './is-outcome-navigation';
import { NOT_FOUND } from './not-found';
import type { PageOutcome } from './page-outcome';
import { resolveOutcome } from './resolve-outcome';

@Component({ template: '' })
class Probe {}

const MOVED: PageOutcome = {
  kind: 'redirect',
  status: HttpStatusCode.MovedPermanently,
  location: '/target?page=2',
};

function answering(outcome: PageOutcome): ResolveFn<unknown> {
  return (_route, state) => injectOutcomeCommand()(outcome, state.url);
}

function routesWith(outcomeResolver: ResolveFn<unknown>): Routes {
  return [
    {
      path: '**',
      canMatch: [isOutcomeNavigation],
      resolve: { outcome: outcomeResolver },
      component: Probe,
    },
    { path: 'moved', resolve: { page: answering(MOVED) }, component: Probe },
    { path: 'missing', resolve: { page: answering(NOT_FOUND) }, component: Probe },
    { path: 'target', component: Probe },
    {
      path: 'down',
      resolve: {
        page: () => {
          const headers = new HttpHeaders({ 'Retry-After': '5' });
          return throwError(() => new HttpErrorResponse({ status: 503, headers }));
        },
      },
      component: Probe,
    },
    {
      path: 'broken',
      resolve: {
        page: () => {
          throw new TypeError('broken resolver');
        },
      },
      component: Probe,
    },
    { path: '**', resolve: { outcome: resolveOutcome }, component: Probe },
  ];
}

interface Setup {
  readonly router: Router;
  readonly init: ResponseInit;
  readonly errors: ErrorHandler;
}

function setUp(platform: 'server' | 'browser', outcomeResolver = resolveOutcome): Setup {
  const init: ResponseInit = { status: HttpStatusCode.Ok, headers: new Headers() };
  const errors = { handleError: vi.fn() };
  TestBed.configureTestingModule({
    providers: [
      provideRouter(
        routesWith(outcomeResolver),
        withNavigationErrorHandler(handleNavigationError),
        withRouterConfig({ resolveNavigationPromiseOnError: true }),
      ),
      provideLocationMocks(),
      { provide: PLATFORM_ID, useValue: platform },
      { provide: RESPONSE_INIT, useValue: platform === 'server' ? init : null },
      { provide: ErrorHandler, useValue: errors },
    ],
  });
  return { router: TestBed.inject(Router), init, errors };
}

function leafOf(router: Router): ActivatedRouteSnapshot {
  let route = router.routerState.snapshot.root;
  while (route.firstChild) {
    route = route.firstChild;
  }
  return route;
}

function headerOf(init: ResponseInit, name: string): string | null {
  return new Headers(init.headers).get(name);
}

describe('outcomes rendered in place of a page', () => {
  it('answers a redirect on the server with its status and Location, at the same URL', async () => {
    const { router, init } = setUp('server');

    await router.navigateByUrl('/moved');

    expect(router.url).toBe('/moved');
    expect(leafOf(router).data['outcome']).toEqual(MOVED);
    expect(init.status).toBe(HttpStatusCode.MovedPermanently);
    expect(headerOf(init, 'Location')).toBe('/target?page=2');
    expect(headerOf(init, 'Cache-Control')).toBe('public, max-age=0, s-maxage=60');
  });

  it('follows a redirect in the browser, replacing the stale history entry', async () => {
    const { router } = setUp('browser');
    const navigate = vi.spyOn(router, 'navigateByUrl');

    await router.navigateByUrl('/moved');

    expect(router.url).toBe('/target?page=2');
    expect(leafOf(router).data).toEqual({});
    expect(navigate).toHaveBeenCalledTimes(1);
  });

  it.each(['server', 'browser'] as const)('renders a 404 in place on the %s', async (platform) => {
    const { router, init } = setUp(platform);

    await router.navigateByUrl('/missing');

    expect(router.url).toBe('/missing');
    expect(leafOf(router).data['outcome']).toEqual(NOT_FOUND);
    expect(init.status).toBe(platform === 'server' ? HttpStatusCode.NotFound : HttpStatusCode.Ok);
  });

  it('answers a 404 for a URL no route knows, without an outcome navigation', async () => {
    const { router, init } = setUp('server');

    await router.navigateByUrl('/nowhere/at/all');

    expect(leafOf(router).data['outcome']).toEqual(NOT_FOUND);
    expect(init.status).toBe(HttpStatusCode.NotFound);
    expect(headerOf(init, 'X-Robots-Tag')).toBe('noindex');
  });

  it('keeps plain navigations off the outcome route', async () => {
    const { router, init } = setUp('server');

    await router.navigateByUrl('/target');

    expect(leafOf(router).routeConfig?.path).toBe('target');
    expect(init.status).toBe(HttpStatusCode.Ok);
  });
});

describe('handleNavigationError', () => {
  it('answers a 503 with the Retry-After of an API that cannot answer', async () => {
    const { router, init, errors } = setUp('server');

    await router.navigateByUrl('/down');

    expect(router.url).toBe('/down');
    expect(leafOf(router).data['outcome']).toEqual({
      kind: 'failure',
      status: HttpStatusCode.ServiceUnavailable,
      retryAfter: '5',
    });
    expect(init.status).toBe(HttpStatusCode.ServiceUnavailable);
    expect(headerOf(init, 'Retry-After')).toBe('5');
    expect(errors.handleError).not.toHaveBeenCalled();
  });

  it('answers and logs a 500 for any other failure', async () => {
    const { router, init, errors } = setUp('browser');

    await router.navigateByUrl('/broken');

    expect(router.url).toBe('/broken');
    expect(leafOf(router).data['outcome']).toMatchObject({
      status: HttpStatusCode.InternalServerError,
    });
    expect(init.status).toBe(HttpStatusCode.Ok);
    expect(errors.handleError).toHaveBeenCalledWith(new TypeError('broken resolver'));
  });

  it('gives up when the error page fails too, instead of looping', async () => {
    const brokenErrorPage = vi.fn(() => {
      throw new Error('broken error page');
    });
    const { router } = setUp('server', brokenErrorPage);

    await expect(router.navigateByUrl('/broken')).resolves.toBe(false);
    expect(brokenErrorPage).toHaveBeenCalledTimes(1);
  });
});
