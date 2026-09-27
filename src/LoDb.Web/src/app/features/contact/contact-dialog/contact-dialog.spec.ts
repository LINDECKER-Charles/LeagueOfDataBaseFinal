import { provideHttpClient } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { provideTransloco } from '@jsverse/transloco';
import { of } from 'rxjs';
import { API_BASE_URL } from '../../../core/api/api-base-url';
import { ContactDialog } from './contact-dialog';

async function render() {
  TestBed.configureTestingModule({
    providers: [
      { provide: API_BASE_URL, useValue: 'https://api.example.com' },
      provideHttpClient(),
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
  return { fixture, host: fixture.nativeElement as HTMLElement };
}

describe('ContactDialog', () => {
  it('shows a visible call to action in the footer', async () => {
    const { host } = await render();

    expect(host.hasAttribute('hidden')).toBe(false);
    expect(host.querySelector('button')?.textContent?.trim()).toBe('contact.form.cta');
  });

  it('opens the contact form in a form-sized dialog named by its heading', async () => {
    const { fixture, host } = await render();

    host.querySelector('button')!.click();
    await fixture.whenStable();
    // The form is loaded on the first click.
    await vi.waitFor(() => expect(document.querySelector('[role=dialog]')).not.toBeNull());

    const dialog = document.querySelector('[role=dialog]');
    expect(dialog?.getAttribute('aria-labelledby')).toBe('lodb-contact-form-heading');
    expect(dialog?.querySelector('lodb-contact-form form')).not.toBeNull();
    expect(dialog?.closest('.cdk-overlay-pane')?.classList).toContain('hx-dialog--form');
  });
});
