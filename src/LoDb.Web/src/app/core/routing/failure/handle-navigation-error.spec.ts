import { HttpStatusCode } from '@angular/common/http';
import { provideLocationMocks } from '@angular/common/testing';
import { Component, ErrorHandler, PLATFORM_ID } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import {
  provideRouter,
  Router,
  withNavigationErrorHandler,
  withRouterConfig,
} from '@angular/router';
import { isOutcomeNavigation } from '../outcome/is-outcome-navigation';
import { resolveOutcome } from '../outcome/resolve-outcome';
import { ChunkReload } from './chunk-reload';
import { handleNavigationError } from './handle-navigation-error';

@Component({ template: '' })
class Probe {}

const MISSING_CHUNK = new TypeError(
  'Failed to fetch dynamically imported module: http://localhost/build/chunk-ABCD1234.js',
);

// A router whose lazy route names a bundle that is gone, and a reload that `reloads` or not.
function setUp(reloads: boolean) {
  const reloadOnce = vi.fn(() => reloads);
  const errors = { handleError: vi.fn() };
  TestBed.configureTestingModule({
    providers: [
      provideRouter(
        [
          {
            path: '**',
            canMatch: [isOutcomeNavigation],
            resolve: { outcome: resolveOutcome },
            component: Probe,
          },
          { path: 'champions', loadComponent: () => Promise.reject(MISSING_CHUNK) },
          { path: '', component: Probe },
        ],
        withNavigationErrorHandler(handleNavigationError),
        withRouterConfig({ resolveNavigationPromiseOnError: true }),
      ),
      provideLocationMocks(),
      { provide: PLATFORM_ID, useValue: 'browser' },
      { provide: ChunkReload, useValue: { reloadOnce } },
      { provide: ErrorHandler, useValue: errors },
    ],
  });
  return { router: TestBed.inject(Router), reloadOnce, errors };
}

describe('handleNavigationError on a lazy chunk that failed to load', () => {
  it('reloads the page at the requested address instead of an error page', async () => {
    const { router, reloadOnce, errors } = setUp(true);

    await expect(router.navigateByUrl('/champions?tags=Mage')).resolves.toBe(false);

    expect(reloadOnce).toHaveBeenCalledExactlyOnceWith('/champions?tags=Mage');
    expect(router.url).toBe('/');
    expect(errors.handleError).not.toHaveBeenCalled();
  });

  it('renders and logs the 500 page once the reload was spent', async () => {
    const { router, errors } = setUp(false);

    await router.navigateByUrl('/champions');

    expect(router.url).toBe('/champions');
    expect(router.routerState.snapshot.root.firstChild?.data['outcome']).toMatchObject({
      status: HttpStatusCode.InternalServerError,
    });
    expect(errors.handleError).toHaveBeenCalledWith(MISSING_CHUNK);
  });
});
