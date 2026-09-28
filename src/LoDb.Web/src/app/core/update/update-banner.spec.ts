import { type WritableSignal, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideTransloco } from '@jsverse/transloco';
import { of } from 'rxjs';
import { PLATFORM } from '../platform/platform';
import type { UpdateState } from '../platform/update-state';
import { ClientUpdate } from './client-update';
import { UpdateBanner } from './update-banner';

interface FakePlatform {
  readonly updateState: WritableSignal<UpdateState>;
  readonly applyUpdate: ReturnType<typeof vi.fn>;
}

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
  const fixture = TestBed.createComponent(UpdateBanner);
  await fixture.whenStable();
  return { fixture, host: fixture.nativeElement as HTMLElement };
}

function buttons(host: HTMLElement): HTMLButtonElement[] {
  return Array.from(host.querySelectorAll('button'));
}

describe('UpdateBanner', () => {
  it('stays empty on the web and while no update is ready', async () => {
    const web = await open(null);
    expect(web.host.textContent?.trim()).toBe('');

    TestBed.resetTestingModule();
    const app = await open({ updateState: signal('downloading'), applyUpdate: vi.fn() });
    expect(app.host.textContent?.trim()).toBe('');
  });

  it('offers to restart on a ready update', async () => {
    const platform = { updateState: signal<UpdateState>('ready'), applyUpdate: vi.fn() };
    const { fixture, host } = await open(platform);

    expect(host.textContent).toContain('update.banner_ready');
    buttons(host)[0]?.click();
    await fixture.whenStable();

    expect(platform.applyUpdate).toHaveBeenCalledTimes(1);
  });

  it('can be put off until the next update', async () => {
    const platform = { updateState: signal<UpdateState>('ready'), applyUpdate: vi.fn() };
    const { fixture, host } = await open(platform);

    buttons(host)[1]?.click();
    await fixture.whenStable();
    expect(host.textContent?.trim()).toBe('');

    platform.updateState.set('downloading');
    await fixture.whenStable();
    platform.updateState.set('ready');
    await fixture.whenStable();
    expect(host.textContent).toContain('update.banner_ready');
  });

  it('gives way to the blocking screen', async () => {
    const { fixture, host } = await open({ updateState: signal('ready'), applyUpdate: vi.fn() });

    TestBed.inject(ClientUpdate).require({
      clientVersion: null,
      minimumVersion: null,
      latestVersion: null,
    });
    await fixture.whenStable();

    expect(host.textContent?.trim()).toBe('');
  });
});
