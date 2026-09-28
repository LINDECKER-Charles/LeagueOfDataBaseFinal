import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { Component, PLATFORM_ID } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { provideTransloco } from '@jsverse/transloco';
import { of } from 'rxjs';
import { ToastService } from '../../../../core/layout/toast/toast-service';
import { ResetPasswordView } from './reset-password-view';

@Component({ template: '' })
class Probe {}

const RESET_URL = '/api/account/reset-password';
const CHECK_URL = '/api/account/reset-password/check';
const STRONG = 'Correct-horse-42';
const FORGOT = '/fr/account/forgot-password';

// Without catalogues, every text renders as its key: the spec reads which message is shown.
async function open(url: string) {
  document.documentElement.lang = 'fr';
  TestBed.configureTestingModule({
    providers: [
      { provide: PLATFORM_ID, useValue: 'browser' },
      provideHttpClient(),
      provideHttpClientTesting(),
      provideRouter([
        { path: ':locale/account/reset-password/:token', component: ResetPasswordView },
        { path: '**', component: Probe },
      ]),
      provideTransloco({
        config: {
          availableLangs: ['fr'],
          defaultLang: 'fr',
          missingHandler: { logMissingKey: false },
        },
        loader: class {
          getTranslation = () => of({});
        },
      }),
    ],
  });
  const harness = await RouterTestingHarness.create(url);
  await harness.fixture.whenStable();
  return { harness, host: harness.routeNativeElement as HTMLElement };
}

// Resolves the pending request, then one turn of the event loop settles what follows it.
async function settle(harness: RouterTestingHarness) {
  await new Promise((resolve) => setTimeout(resolve));
  await harness.fixture.whenStable();
}

async function submit(harness: RouterTestingHarness, password: string) {
  const host = harness.routeNativeElement as HTMLElement;
  for (const id of ['reset-password', 'reset-confirmation']) {
    const field = host.querySelector<HTMLInputElement>(`#${id}`)!;
    field.value = password;
    field.dispatchEvent(new Event('input'));
  }
  host.querySelector('form')!.dispatchEvent(new Event('submit', { cancelable: true }));
  await harness.fixture.whenStable();
}

interface Refusal {
  readonly status: number;
  readonly code: string;
}

const EXPIRED: Refusal = { status: 400, code: 'invalid-token' };
const LIMITED: Refusal = { status: 429, code: 'rate-limited' };

// Answers the check the page sends when it opens: 204 for a usable link, else the refusal.
async function answerCheck(harness: RouterTestingHarness, refusal: Refusal | null) {
  const request = TestBed.inject(HttpTestingController).expectOne(CHECK_URL);
  expect(request.request.body).toEqual({ userId: 7, token: 'token' });
  if (refusal === null) {
    request.flush(null, { status: 204, statusText: 'No Content' });
  } else {
    request.flush({ code: refusal.code }, { status: refusal.status, statusText: 'Refused' });
  }
  await settle(harness);
}

function toasted(): string[] {
  return TestBed.inject(ToastService)
    .toasts()
    .map(({ kind, message }) => `${kind}:${message}`);
}

describe('ResetPasswordView', () => {
  let lang: string;

  beforeEach(() => {
    lang = document.documentElement.lang;
  });

  // The specs share one document: the locale of this page must not leak into the next ones.
  afterEach(() => {
    document.documentElement.lang = lang;
    document.documentElement.removeAttribute('dir');
  });

  it('sends a damaged link back to a new request at once, as the legacy page did', async () => {
    await open('/fr/account/reset-password/token?user=abc');
    await new Promise((resolve) => setTimeout(resolve));

    expect(TestBed.inject(Router).url).toBe(FORGOT);
    expect(toasted()).toEqual(['error:auth.flash.reset_error']);
    TestBed.inject(HttpTestingController).expectNone(CHECK_URL);
  });

  it('sends a link found expired on opening back to a new request, formless', async () => {
    const { harness, host } = await open('/fr/account/reset-password/token?user=7');
    expect(host.querySelector('form')).toBeNull();

    await answerCheck(harness, EXPIRED);

    expect(TestBed.inject(Router).url).toBe(FORGOT);
    expect(toasted()).toEqual(['error:auth.flash.reset_error']);
  });

  it('shows the form once the link is found usable, or when the check fails', async () => {
    const usable = await open('/fr/account/reset-password/token?user=7');
    await answerCheck(usable.harness, null);
    expect(usable.host.querySelector('form')).not.toBeNull();

    TestBed.resetTestingModule();
    const unchecked = await open('/fr/account/reset-password/token?user=7');
    await answerCheck(unchecked.harness, LIMITED);
    expect(unchecked.host.querySelector('form')).not.toBeNull();
    expect(toasted()).toEqual([]);
  });

  it('sends a link found expired when sent back to a new request, with the same toast', async () => {
    const { harness } = await open('/fr/account/reset-password/token?user=7');
    await answerCheck(harness, null);

    await submit(harness, STRONG);
    const request = TestBed.inject(HttpTestingController).expectOne(RESET_URL);
    expect(request.request.body).toEqual({ userId: 7, token: 'token', password: STRONG });
    request.flush({ code: 'invalid-token' }, { status: 400, statusText: 'Bad Request' });
    await settle(harness);

    expect(TestBed.inject(Router).url).toBe(FORGOT);
    expect(toasted()).toEqual(['error:auth.flash.reset_error']);
  });
});
