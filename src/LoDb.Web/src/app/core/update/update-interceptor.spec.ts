import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { firstValueFrom } from 'rxjs';
import { ClientUpdate } from './client-update';
import { updateInterceptor } from './update-interceptor';

function setUp() {
  TestBed.configureTestingModule({
    providers: [
      provideHttpClient(withInterceptors([updateInterceptor])),
      provideHttpClientTesting(),
    ],
  });
  return {
    http: TestBed.inject(HttpClient),
    backend: TestBed.inject(HttpTestingController),
    update: TestBed.inject(ClientUpdate),
  };
}

describe('updateInterceptor', () => {
  it('blocks the app on a 426 and still fails the request', async () => {
    const { http, backend, update } = setUp();
    const request = firstValueFrom(http.get('/api/account/me'));

    backend
      .expectOne('/api/account/me')
      .flush(
        { code: 'client-upgrade-required', minimumVersion: '1.4.0', clientVersion: '1.3.0' },
        { status: 426, statusText: 'Upgrade Required' },
      );

    await expect(request).rejects.toMatchObject({ status: 426 });
    expect(update.requirement()).toEqual({
      clientVersion: '1.3.0',
      minimumVersion: '1.4.0',
      latestVersion: null,
    });
  });

  it('leaves the app alone on answers and other failures', async () => {
    const { http, backend, update } = setUp();
    const ok = firstValueFrom(http.get('/api/meta'));
    const refused = firstValueFrom(http.get('/api/account/me'));

    backend.expectOne('/api/meta').flush({});
    backend.expectOne('/api/account/me').flush({}, { status: 401, statusText: 'Unauthorized' });

    await expect(ok).resolves.toEqual({});
    await expect(refused).rejects.toMatchObject({ status: 401 });
    expect(update.requirement()).toBeNull();
  });
});
