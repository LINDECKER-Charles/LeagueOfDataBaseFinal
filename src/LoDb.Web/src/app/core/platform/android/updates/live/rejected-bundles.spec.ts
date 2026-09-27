import { TestBed } from '@angular/core/testing';
import { ANDROID_PLUGINS } from '../../native/android-plugins-token';
import { FakeAndroidPlugins } from '../../testing/fake-android-plugins';
import { RejectedBundles } from './rejected-bundles';

describe('RejectedBundles', () => {
  let native: FakeAndroidPlugins;

  function rejected(): RejectedBundles {
    TestBed.configureTestingModule({
      providers: [{ provide: ANDROID_PLUGINS, useValue: native.plugins }],
    });
    return TestBed.inject(RejectedBundles);
  }

  beforeEach(() => {
    native = new FakeAndroidPlugins();
  });

  it('keeps the rolled back bundles across starts, in the secure storage', async () => {
    await rejected().add('2.4.0');
    TestBed.resetTestingModule();

    expect(await rejected().list()).toEqual(['2.4.0']);
    expect(native.stored.get('lodb.liveUpdate.rejected')).toBe('["2.4.0"]');
  });

  it('lists a bundle once however often it failed', async () => {
    const bundles = rejected();
    await bundles.add('2.4.0');
    await bundles.add('2.4.1');
    await bundles.add('2.4.0');

    expect(await bundles.list()).toEqual(['2.4.1', '2.4.0']);
  });

  it('forgets the oldest bundles beyond twenty', async () => {
    const bundles = rejected();
    for (let patch = 0; patch < 25; patch++) {
      await bundles.add(`2.4.${patch}`);
    }

    const ids = await bundles.list();
    expect(ids).toHaveLength(20);
    expect(ids[0]).toBe('2.4.5');
    expect(ids.at(-1)).toBe('2.4.24');
  });

  it.each(['not json', '{"id":"2.4.0"}', '[1,"2.4.0",null]'])(
    'reads only the ids of a damaged entry (%s)',
    async (stored) => {
      native.stored.set('lodb.liveUpdate.rejected', stored);

      expect(await rejected().list()).toEqual(stored.includes('[') ? ['2.4.0'] : []);
    },
  );

  it('reads nothing, and fails nothing, when the Keystore fails', async () => {
    native.storageFailure = new Error('Keystore unavailable');
    const bundles = rejected();

    await expect(bundles.add('2.4.0')).resolves.toBeUndefined();
    expect(await bundles.list()).toEqual([]);
  });
});
