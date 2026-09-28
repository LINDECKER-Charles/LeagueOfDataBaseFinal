import { type WritableSignal, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideTransloco } from '@jsverse/transloco';
import { of } from 'rxjs';
import { PLATFORM } from '../platform/platform';
import type { UpdateState } from '../platform/update-state';
import { ClientUpdate } from './client-update';
import { UpdateScreen } from './update-screen';

interface FakePlatform {
  readonly updateState: WritableSignal<UpdateState>;
  readonly applyUpdate: ReturnType<typeof vi.fn>;
}

// Without catalogues, every text renders as its key: the spec reads which one is shown.
async function open(platform: FakePlatform | null) {
  TestBed.configureTestingModule({
    providers: [
      platform === null ? [] : { provide: PLATFORM, useValue: platform },
      provideTransloco({
        config: { defaultLang: 'en', missingHandler: { logMissingKey: false } },
        loader: class {
          getTranslation = () => of({});
        },
      }),
    ],
  });
  const page = document.createElement('main');
  document.body.append(page);
  const fixture = TestBed.createComponent(UpdateScreen);
  document.body.append(fixture.nativeElement as HTMLElement);
  await fixture.whenStable();
  return { fixture, page, host: fixture.nativeElement as HTMLElement };
}

function fakePlatform(state: UpdateState = 'none'): FakePlatform {
  return { updateState: signal(state), applyUpdate: vi.fn(async () => undefined) };
}

function block(): void {
  TestBed.inject(ClientUpdate).require({
    clientVersion: '1.3.0',
    minimumVersion: '1.4.0',
    latestVersion: '1.5.2',
  });
}

describe('UpdateScreen', () => {
  afterEach(() => {
    document.body.replaceChildren();
  });

  it('shows nothing while the API accepts the version, as on the web', async () => {
    const { host, page } = await open(null);

    expect(host.textContent?.trim()).toBe('');
    expect(page.hasAttribute('inert')).toBe(false);
  });

  it('blocks the whole application once the API answered 426', async () => {
    const { fixture, host, page } = await open(fakePlatform());

    block();
    await fixture.whenStable();

    const dialog = host.querySelector('[role="alertdialog"]');
    expect(dialog?.getAttribute('aria-modal')).toBe('true');
    expect(host.textContent).toContain('update.required_title');
    expect(host.textContent).toContain('update.required_versions');
    expect(host.textContent).toContain('update.waiting');
    expect(page.hasAttribute('inert')).toBe(true);
    expect(host.hasAttribute('inert')).toBe(false);
    expect(document.activeElement?.id).toBe('lodb-update-title');
  });

  it('follows the download, then restarts on the ready update', async () => {
    const platform = fakePlatform('downloading');
    const { fixture, host } = await open(platform);
    block();
    await fixture.whenStable();
    expect(host.textContent).toContain('update.downloading');
    expect(host.querySelector('button')).toBeNull();

    platform.updateState.set('ready');
    await fixture.whenStable();
    host.querySelector('button')?.click();
    await fixture.whenStable();

    expect(platform.applyUpdate).toHaveBeenCalledTimes(1);
  });

  it('says so when the restart fails', async () => {
    const platform = fakePlatform('ready');
    platform.applyUpdate.mockRejectedValue(new Error('no-update'));
    const { fixture, host } = await open(platform);
    block();
    await fixture.whenStable();

    host.querySelector('button')?.click();
    await fixture.whenStable();

    expect(host.querySelector('[role="alert"]')?.textContent).toContain('update.restart_failed');
  });
});
