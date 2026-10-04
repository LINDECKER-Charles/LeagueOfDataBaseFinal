import { HttpErrorResponse, HttpStatusCode, provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { RESPONSE_INIT } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { firstValueFrom } from 'rxjs';
import { CACHE_CONTROL } from '../../routing/response/cache-control';
import { TranslocoHttpLoader } from './transloco-http-loader';

describe('TranslocoHttpLoader', () => {
  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
  });

  afterEach(() => TestBed.inject(HttpTestingController).verify());

  it.each([
    ['fr', 'i18n/fr.json'],
    ['zh-hans', 'i18n/zh-hans.json'],
    ['seo/fr', 'i18n/seo/fr.json'],
  ])('loads %s from the relative URL %s', async (path, url) => {
    const loaded = firstValueFrom(TestBed.inject(TranslocoHttpLoader).getTranslation(path));

    TestBed.inject(HttpTestingController).expectOne(url).flush({ title: 'ok' });

    await expect(loaded).resolves.toEqual({ title: 'ok' });
  });

  // Transloco asks the main and the fallback loader for `items/en` at once under `en`.
  it('shares one request between concurrent loads of a path', async () => {
    const loader = TestBed.inject(TranslocoHttpLoader);
    const both = Promise.all([
      firstValueFrom(loader.getTranslation('items/en')),
      firstValueFrom(loader.getTranslation('items/en')),
    ]);

    TestBed.inject(HttpTestingController).expectOne('i18n/items/en.json').flush({ a: 'b' });

    await expect(both).resolves.toEqual([{ a: 'b' }, { a: 'b' }]);
  });

  it('requests a failed path again', async () => {
    const loader = TestBed.inject(TranslocoHttpLoader);
    const http = TestBed.inject(HttpTestingController);
    const failed = firstValueFrom(loader.getTranslation('items/en'));
    http
      .expectOne('i18n/items/en.json')
      .flush('', { status: HttpStatusCode.ServiceUnavailable, statusText: 'Unavailable' });
    await expect(failed).rejects.toBeInstanceOf(HttpErrorResponse);

    const retried = firstValueFrom(loader.getTranslation('items/en'));
    http.expectOne('i18n/items/en.json').flush({ a: 'b' });

    await expect(retried).resolves.toEqual({ a: 'b' });
  });

  describe('in a server render', () => {
    let init: ResponseInit;

    beforeEach(() => {
      init = { headers: new Headers({ 'Cache-Control': CACHE_CONTROL.latest }) };
      TestBed.configureTestingModule({ providers: [{ provide: RESPONSE_INIT, useValue: init }] });
    });

    const cacheControl = () => new Headers(init.headers).get('Cache-Control');
    const load = (path: string) =>
      firstValueFrom(TestBed.inject(TranslocoHttpLoader).getTranslation(path));

    it('keeps the response out of caches once a catalogue fails, and still fails', async () => {
      const loaded = load('api/fr');

      TestBed.inject(HttpTestingController)
        .expectOne('i18n/api/fr.json')
        .flush('', { status: HttpStatusCode.ServiceUnavailable, statusText: 'Unavailable' });

      await expect(loaded).rejects.toBeInstanceOf(HttpErrorResponse);
      expect(cacheControl()).toBe(CACHE_CONTROL.private);
    });

    it('leaves the cache of the response alone when the catalogue loads', async () => {
      const loaded = load('api/fr');

      TestBed.inject(HttpTestingController).expectOne('i18n/api/fr.json').flush({ nav: {} });

      await loaded;
      expect(cacheControl()).toBe(CACHE_CONTROL.latest);
    });
  });
});
