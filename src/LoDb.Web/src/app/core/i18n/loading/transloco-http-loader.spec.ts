import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { firstValueFrom } from 'rxjs';
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
});
