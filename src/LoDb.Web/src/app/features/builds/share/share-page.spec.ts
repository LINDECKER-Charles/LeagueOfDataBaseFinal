import {
  type HttpInterceptorFn,
  HttpResponse,
  HttpStatusCode,
  provideHttpClient,
  withInterceptors,
} from '@angular/common/http';
import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { provideTransloco, type Translation } from '@jsverse/transloco';
import { of } from 'rxjs';
import { API_BASE_URL } from '../../../core/api/api-base-url';
import type { SharedBuild } from '../../../core/api/generated/models/shared-build';
import { AuthSession } from '../../../core/auth/session/auth-session';
import type { SessionStatus } from '../../../core/auth/session/session-status';
import { CANONICAL_ORIGIN } from '../../../core/seo/canonical-origin';
import { Seo } from '../../../core/seo/seo';
import { SharePage } from './share-page';
import { sharedBuildOf } from './testing/shared-build-of';

const ORIGIN = 'https://league-of-data-base.com';
const CATALOGUES: Record<string, Translation> = {
  fr: {
    build: {
      show: {
        by: 'Par {{ name }}',
        patch: 'Forgé sur le patch {{ version }}',
        patch_current: 'Forgé sur le patch {{ version }} — actuel : {{ current }}',
        mode: 'Mode de jeu',
        missing: 'Indisponible sur ce patch',
        meta_fallback: 'Runes et ordre d’achat pour {{ champion }}.',
        copy: 'Copier le lien',
      },
      mode: { sr: 'Faille de l’invocateur', aram: 'ARAM' },
    },
    community: { supporter: { badge: 'Soutien' }, vote: { up: 'Pour', down: 'Contre' } },
  },
};

// The API of the browser's second read: the build again, with the reader's own vote.
function simulatedApi(fresh: SharedBuild, requested: string[]): HttpInterceptorFn {
  return (request) => {
    requested.push(request.urlWithParams);
    return of(new HttpResponse({ status: HttpStatusCode.Ok, url: request.url, body: fresh }));
  };
}

async function visit(build: SharedBuild, status: SessionStatus = 'unknown', fresh = build) {
  const requested: string[] = [];
  document.documentElement.lang = 'fr';
  TestBed.configureTestingModule({
    providers: [
      { provide: CANONICAL_ORIGIN, useValue: ORIGIN },
      { provide: API_BASE_URL, useValue: '' },
      { provide: AuthSession, useValue: { status: signal(status) } },
      provideHttpClient(withInterceptors([simulatedApi(fresh, requested)])),
      provideRouter([
        {
          path: 'b/:token',
          resolve: { shared: () => ({ build, locale: 'fr' }) },
          component: SharePage,
        },
      ]),
      provideTransloco({
        config: {
          availableLangs: ['en', 'fr'],
          defaultLang: 'fr',
          missingHandler: { logMissingKey: false },
          prodMode: true,
        },
        loader: class {
          getTranslation = (path: string) => of(CATALOGUES[path] ?? {});
        },
      }),
    ],
  });
  const apply = vi.spyOn(TestBed.inject(Seo), 'apply');
  const harness = await RouterTestingHarness.create(`/b/${build.shareToken}`);
  await harness.fixture.whenStable();
  harness.fixture.detectChanges();
  return { apply, requested, host: harness.routeNativeElement as HTMLElement };
}

function textOf(host: Element, selector: string): string {
  return host.querySelector(selector)?.textContent?.replace(/\s+/g, ' ').trim() ?? '';
}

describe('SharePage', () => {
  let lang: string;

  beforeEach(() => {
    lang = document.documentElement.lang;
  });

  afterEach(() => {
    document.documentElement.lang = lang;
    document.head.querySelectorAll('[data-lodb-seo]').forEach((element) => element.remove());
  });

  it('shows the seal, the author, the mode and the patch the build was forged on', async () => {
    const { host } = await visit(sharedBuildOf());

    expect(textOf(host, '.bshare-head h1')).toBe('Mid burst');
    expect(textOf(host, '.bshare-head .eyebrow')).toBe('Ahri · the Nine-Tailed Fox');
    expect(textOf(host, 'lodb-owner-credit')).toBe('Par Faker#KR1');
    expect(host.querySelector('lodb-owner-credit a')?.getAttribute('href')).toBe('/fr/u/Faker');
    expect(textOf(host, '[data-mode]')).toBe('ARAM');
    expect(host.querySelector('[data-mode]')?.getAttribute('title')).toBe('Mode de jeu');
    expect(textOf(host, '[data-version]')).toBe('Forgé sur le patch 15.14.1 — actuel : 16.19.1');
    expect(textOf(host, '[lang="fr-FR"]')).toBe('French');
  });

  it('names the patch alone when it is the current one', async () => {
    const { host } = await visit(sharedBuildOf({ patchMismatch: false }));

    expect(textOf(host, '[data-version]')).toBe('Forgé sur le patch 15.14.1');
  });

  it('keeps the ghosts in place, dimmed and titled as unavailable', async () => {
    const { host } = await visit(sharedBuildOf());
    const items = [...host.querySelectorAll('.bsteps-node .bshare-item')];

    expect(host.querySelectorAll('.bsteps-node h3')).toHaveLength(2);
    expect(items.map((item) => item.getAttribute('data-item-id'))).toEqual([
      '1056',
      '9999',
      '3089',
    ]);
    expect(items[1].classList).toContain('forge-ghost');
    expect(items[1].getAttribute('title')).toBe('Indisponible sur ce patch');
    expect(items[1].textContent?.trim()).toBe('99');
    expect(host.querySelector('.keystone__icon')?.classList).toContain('forge-ghost');
    expect(textOf(host, '.bshare-tree')).toBe('Domination');
  });

  it('scores a public build and offers its link to copy', async () => {
    const { host } = await visit(sharedBuildOf());

    expect(textOf(host, '.vote-score')).toBe('+3');
    expect(textOf(host, 'lodb-copy-link')).toContain('Copier le lien');
  });

  it('shows no score on a private build', async () => {
    const { host, requested } = await visit(
      sharedBuildOf({ isPublic: false, vote: null }),
      'authenticated',
    );

    expect(host.querySelector('lodb-vote-score')).toBeNull();
    expect(requested).toEqual([]);
  });

  it('reads the build again once signed in, for the reader’s own vote', async () => {
    const build = sharedBuildOf();
    const fresh = sharedBuildOf({ vote: { score: 4, myVote: 1 } });

    const { host, requested } = await visit(build, 'authenticated', fresh);

    expect(requested).toEqual([`/api/share/${build.shareToken}`]);
    expect(textOf(host, '.vote-score')).toBe('+4');
    expect(host.querySelector('.vote-arrow--on-up')).not.toBeNull();
  });

  it('writes an unlisted head in the build’s language, without structured data', async () => {
    const { apply } = await visit(sharedBuildOf());

    expect(apply).toHaveBeenLastCalledWith({
      kind: 'share',
      title: 'Mid burst · Ahri',
      description: 'Roam after six.',
      locale: 'fr',
    });
    expect(document.head.querySelector('meta[name="robots"]')?.getAttribute('content')).toContain(
      'noindex',
    );
    expect(document.head.querySelector('link[rel="canonical"]')).toBeNull();
    expect(document.head.querySelector('script[type="application/ld+json"]')).toBeNull();
  });

  it('describes a build without a description by its champion', async () => {
    const { apply } = await visit(sharedBuildOf({ description: '  ' }));

    expect(apply).toHaveBeenLastCalledWith(
      expect.objectContaining({ description: 'Runes et ordre d’achat pour Ahri.' }),
    );
  });
});
