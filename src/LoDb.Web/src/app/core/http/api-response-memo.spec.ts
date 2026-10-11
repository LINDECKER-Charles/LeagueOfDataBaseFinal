import {
  HttpErrorResponse,
  type HttpEvent,
  HttpRequest,
  HttpResponse,
  HttpStatusCode,
} from '@angular/common/http';
import { type Observable, firstValueFrom, of, throwError } from 'rxjs';
import { ApiResponseMemo } from './api-response-memo';

const META = 'http://api:8080/api/meta';
const TTL_MS = 60_000;

describe('ApiResponseMemo', () => {
  let now: number;
  let memo: ApiResponseMemo;
  let fetch: ReturnType<
    typeof vi.fn<(request: HttpRequest<unknown>) => Observable<HttpEvent<unknown>>>
  >;

  const get = new HttpRequest('GET', META);
  const answered = (body: unknown) => of(new HttpResponse({ body, status: HttpStatusCode.Ok }));
  const failed = (status: number) => throwError(() => new HttpErrorResponse({ status }));
  const body = async () =>
    ((await firstValueFrom(memo.answer(get, fetch))) as HttpResponse<unknown>).body;

  beforeEach(() => {
    now = 0;
    memo = new ApiResponseMemo(new Set([META]), TTL_MS, () => now);
    fetch = vi.fn(() => answered({ latest: '16.1.1' }));
  });

  it('covers a GET of its URLs only', () => {
    expect(memo.covers(get)).toBe(true);
    expect(memo.covers(new HttpRequest('POST', META, null))).toBe(false);
    expect(memo.covers(new HttpRequest('GET', `${META}?x=1`))).toBe(false);
    expect(memo.covers(new HttpRequest('GET', 'http://api:8080/api/catalog'))).toBe(false);
  });

  it('answers every render from one response while it is fresh, then asks again', async () => {
    await body();
    now = TTL_MS - 1;
    await body();
    expect(fetch).toHaveBeenCalledOnce();

    now = TTL_MS;
    fetch.mockReturnValueOnce(answered({ latest: '16.2.1' }));
    await expect(body()).resolves.toEqual({ latest: '16.2.1' });
    expect(fetch).toHaveBeenCalledTimes(2);
  });

  it('shares a frozen body: no render can change what the others read', async () => {
    const shared = (await body()) as { latest: string };

    expect(Object.isFrozen(shared)).toBe(true);
  });

  it.each([0, HttpStatusCode.BadGateway, HttpStatusCode.ServiceUnavailable])(
    'serves the last good copy when the API cannot answer (%s)',
    async (status) => {
      await body();
      now = TTL_MS;
      fetch.mockReturnValueOnce(failed(status));

      await expect(body()).resolves.toEqual({ latest: '16.1.1' });
    },
  );

  it('fails like the API without a copy, or on an answer that is not a failure to answer', async () => {
    fetch.mockReturnValueOnce(failed(HttpStatusCode.ServiceUnavailable));
    await expect(body()).rejects.toMatchObject({ status: HttpStatusCode.ServiceUnavailable });

    await body();
    now = TTL_MS;
    fetch.mockReturnValueOnce(failed(HttpStatusCode.NotFound));
    await expect(body()).rejects.toMatchObject({ status: HttpStatusCode.NotFound });
  });

  it('never keeps a failure: the next render asks again', async () => {
    fetch.mockReturnValueOnce(failed(HttpStatusCode.ServiceUnavailable));
    await expect(body()).rejects.toBeTruthy();

    await expect(body()).resolves.toEqual({ latest: '16.1.1' });
    expect(fetch).toHaveBeenCalledTimes(2);
  });
});
