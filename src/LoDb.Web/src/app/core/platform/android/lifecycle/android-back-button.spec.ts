import { Dialog } from '@angular/cdk/dialog';
import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { ANDROID_PLUGINS } from '../native/android-plugins-token';
import { FakeAndroidPlugins } from '../testing/fake-android-plugins';
import { AndroidBackButton } from './android-back-button';

@Component({ template: '<p>Dialog</p>' })
class Content {}

describe('AndroidBackButton', () => {
  let native: FakeAndroidPlugins;
  let back: ReturnType<typeof vi.spyOn>;

  beforeEach(() => {
    native = new FakeAndroidPlugins();
    TestBed.configureTestingModule({
      providers: [{ provide: ANDROID_PLUGINS, useValue: native.plugins }],
    });
    back = vi.spyOn(window.history, 'back').mockImplementation(() => undefined);
    TestBed.inject(AndroidBackButton).start();
  });

  afterEach(() => {
    TestBed.inject(Dialog).closeAll();
    vi.restoreAllMocks();
  });

  it('closes the dialog on top first, as Escape does', async () => {
    const dialog = TestBed.inject(Dialog);
    const below = dialog.open(Content);
    const top = dialog.open(Content);

    native.emit('backButton', { canGoBack: true });

    await vi.waitFor(() => expect(dialog.openDialogs).toEqual([below]));
    expect(top.componentInstance).toBeNull();
    expect(back).not.toHaveBeenCalled();
  });

  it('goes back in the history when there is a page to go back to', async () => {
    native.emit('backButton', { canGoBack: true });

    await vi.waitFor(() => expect(back).toHaveBeenCalledOnce());
    expect(native.minimizes).toBe(0);
  });

  it('moves the app to the background on the first page, rather than quitting', async () => {
    native.emit('backButton', { canGoBack: false });

    await vi.waitFor(() => expect(native.minimizes).toBe(1));
    expect(back).not.toHaveBeenCalled();
  });
});
