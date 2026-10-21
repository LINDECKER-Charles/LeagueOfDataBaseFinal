import {
  HttpClient,
  HttpContext,
  provideHttpClient,
  withInterceptors,
  ɵwithHttpTransferCache as withHttpTransferCache,
} from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { firstValueFrom } from 'rxjs';
import { BYPASS_TRANSFER_CACHE } from './bypass-transfer-cache';
import { bypassTransferCacheInterceptor } from './bypass-transfer-cache-interceptor';

const META_URL = 'https://league-of-data-base.com/api/meta';
const EMBEDDED = { latest: '16.20.1' };
const FRESH = { latest: '16.21.1' };

// The transfer cache stores what a server render reads: `ngServerMode` is a build-time flag
// of the server bundle, read when the request runs.
async function renderOnServer(http: HttpTestingController): Promise<void> {
  Reflect.set(globalThis, 'ngServerMode', true);
  try {
    const answer = firstValueFrom(TestBed.inject(HttpClient).get(META_URL));
    http.expectOne(META_URL).flush(EMBEDDED);
    await answer;
  } finally {
    Reflect.deleteProperty(globalThis, 'ngServerMode');
  }
}

describe('bypassTransferCacheInterceptor', () => {
  let http: HttpTestingController;

  beforeEach(async () => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([bypassTransferCacheInterceptor])),
        provideHttpClientTesting(),
        withHttpTransferCache({}),
      ],
    });
    http = TestBed.inject(HttpTestingController);
    await renderOnServer(http);
  });

  afterEach(() => http.verify());

  it('lets the transfer cache answer an unmarked request while the page hydrates', async () => {
    const answer = await firstValueFrom(TestBed.inject(HttpClient).get(META_URL));

    expect(answer).toEqual(EMBEDDED);
    http.expectNone(META_URL);
  });

  it('sends a marked request to the network, the page embedding its answer or not', async () => {
    const context = new HttpContext().set(BYPASS_TRANSFER_CACHE, true);
    const answer = firstValueFrom(TestBed.inject(HttpClient).get(META_URL, { context }));

    const request = http.expectOne(META_URL);
    request.flush(FRESH);

    expect(request.request.transferCache).toBe(false);
    expect(await answer).toEqual(FRESH);
  });
});
