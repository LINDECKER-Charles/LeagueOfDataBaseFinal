import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { PLATFORM_ID } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideTransloco } from '@jsverse/transloco';
import { of } from 'rxjs';
import { API_BASE_URL } from '../../../core/api/api-base-url';
import { ToastService } from '../../../core/layout/toast/toast-service';
import { ContactDialog } from '../contact-dialog/contact-dialog';

const API = 'https://api.example.com';
const ENDPOINT = `${API}/api/contact`;
const MESSAGE = 'La page des runes ne charge plus depuis ce matin.';

// Without catalogues, every text renders as its key: the spec reads which one is shown.
async function openForm() {
  document.documentElement.lang = 'fr';
  TestBed.configureTestingModule({
    providers: [
      { provide: PLATFORM_ID, useValue: 'browser' },
      { provide: API_BASE_URL, useValue: API },
      provideHttpClient(),
      provideHttpClientTesting(),
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
  const fixture = TestBed.createComponent(ContactDialog);
  await fixture.whenStable();
  (fixture.nativeElement as HTMLElement).querySelector('button')!.click();
  // The form is loaded on the first click, then rendered with its reasons.
  const form = await vi.waitFor(() => {
    const opened = document.querySelector<HTMLFormElement>('lodb-contact-form form');
    expect(opened?.querySelectorAll('option').length).toBeGreaterThan(0);
    return opened!;
  });
  return { fixture, form };
}

function fill(form: HTMLFormElement, values: Readonly<Record<string, string>>): void {
  for (const [name, value] of Object.entries(values)) {
    const control = form.elements.namedItem(name) as HTMLInputElement;
    control.value = value;
  }
}

// The call leaves as soon as the form is sent.
function submit(form: HTMLFormElement) {
  form.dispatchEvent(new Event('submit', { cancelable: true }));
  return TestBed.inject(HttpTestingController).expectOne(ENDPOINT);
}

function texts(selector: string): string[] {
  return [...document.querySelectorAll(selector)].map((node) => node.textContent?.trim() ?? '');
}

describe('ContactForm', () => {
  let lang: string;

  beforeEach(() => {
    lang = document.documentElement.lang;
  });

  afterEach(() => {
    document.documentElement.lang = lang;
    document.documentElement.removeAttribute('dir');
    TestBed.inject(HttpTestingController).verify();
  });

  it('offers the four reasons of the legacy form, the bug first', async () => {
    const { form } = await openForm();
    const select = form.elements.namedItem('category') as HTMLSelectElement;

    expect([...select.options].map((option) => option.value)).toEqual([
      'bug',
      'feedback',
      'review',
      'commercial',
    ]);
    expect(select.value).toBe('bug');
    expect(document.getElementById('lodb-contact-form-heading')?.textContent).toContain(
      'contact.form.title',
    );
  });

  it('hides its honeypot from people and from the tab order', async () => {
    const { form } = await openForm();
    const trap = form.elements.namedItem('website') as HTMLInputElement;

    expect(trap.tabIndex).toBe(-1);
    expect(trap.closest('[aria-hidden=true]')).not.toBeNull();
  });

  it('sends the message in the locale of the page, then closes on a toast', async () => {
    const { form } = await openForm();
    fill(form, { category: 'review', email: 'visiteur@example.test', message: MESSAGE });

    const request = submit(form);

    expect(request.request.body).toEqual({
      locale: 'fr',
      category: 'review',
      name: '',
      email: 'visiteur@example.test',
      subject: '',
      message: MESSAGE,
      website: '',
    });
    request.flush('', { status: 204, statusText: 'No Content' });
    await vi.waitFor(() => expect(document.querySelector('lodb-contact-form')).toBeNull());
    expect(TestBed.inject(ToastService).toasts()).toEqual([
      expect.objectContaining({ kind: 'success', message: 'contact.flash.sent' }),
    ]);
  });

  it('marks each refused field with the text of the legacy form', async () => {
    const { fixture, form } = await openForm();
    fill(form, { email: 'x', message: 'court' });

    const request = submit(form);
    request.flush(
      JSON.stringify({
        code: 'validation-failed',
        errors: { email: ['invalid'], message: ['too-short'], subject: ['too-long'] },
      }),
      { status: 400, statusText: 'Bad Request' },
    );
    await vi.waitFor(() => expect(texts('.contact-field-error')).toHaveLength(3));
    await fixture.whenStable();

    expect(texts('.contact-field-error')).toEqual([
      'contact.error.email',
      'contact.flash.error',
      'contact.error.message_short',
    ]);
    expect(document.querySelector('lodb-contact-form')).not.toBeNull();
    expect((form.elements.namedItem('message') as HTMLTextAreaElement).value).toBe('court');
  });

  it('asks to wait once the limit ran out, the dialog kept open', async () => {
    const { form } = await openForm();
    fill(form, { email: 'visiteur@example.test', message: MESSAGE });

    const request = submit(form);
    request.flush(JSON.stringify({ code: 'rate-limited' }), {
      status: 429,
      statusText: 'Too Many Requests',
    });
    await vi.waitFor(() => expect(TestBed.inject(ToastService).toasts()).toHaveLength(1));

    expect(TestBed.inject(ToastService).toasts()[0]).toEqual(
      expect.objectContaining({ kind: 'warning', message: 'contact.flash.throttled' }),
    );
    expect(document.querySelector('lodb-contact-form')).not.toBeNull();
  });

  it('says the form failed when the API does not answer', async () => {
    const { form } = await openForm();
    fill(form, { email: 'visiteur@example.test', message: MESSAGE });

    const request = submit(form);
    request.error(new ProgressEvent('error'));
    await vi.waitFor(() => expect(texts('.contact-error')).toEqual(['contact.flash.error']));

    expect(TestBed.inject(ToastService).toasts()).toHaveLength(0);
  });
});
