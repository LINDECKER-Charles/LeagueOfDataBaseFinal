import { TestBed } from '@angular/core/testing';
import { ANDROID_PLUGINS } from '../native/android-plugins-token';
import { FakeAndroidPlugins } from '../testing/fake-android-plugins';
import type { PendingGoogleSignIn } from './pending-google-sign-in';
import { PendingGoogleSignIns } from './pending-google-sign-ins';

const KEY = 'lodb.google-sign-in';
const START = Date.UTC(2026, 8, 26, 12);

const flow: PendingGoogleSignIn = {
  clientId: 'client',
  redirectUri: 'https://league-of-data-base.com/app/oauth/google',
  state: 'state-1',
  verifier: 'verifier-1',
  landingUrl: '/fr/account/profile',
  locale: 'fr',
  isRemembered: false,
  startedAt: START,
};

describe('PendingGoogleSignIns', () => {
  let native: FakeAndroidPlugins;
  let pending: PendingGoogleSignIns;
  let now: number;

  beforeEach(() => {
    native = new FakeAndroidPlugins();
    now = START;
    vi.spyOn(Date, 'now').mockImplementation(() => now);
    TestBed.configureTestingModule({
      providers: [{ provide: ANDROID_PLUGINS, useValue: native.plugins }],
    });
    pending = TestBed.inject(PendingGoogleSignIns);
  });

  afterEach(() => vi.restoreAllMocks());

  it('keeps the flow in the secure storage, for a return that starts the app cold', async () => {
    await pending.save(flow);

    expect(JSON.parse(native.stored.get(KEY) ?? 'null')).toEqual(flow);
  });

  it('hands the flow back once only', async () => {
    await pending.save(flow);

    await expect(pending.take()).resolves.toEqual(flow);
    await expect(pending.take()).resolves.toBeNull();
  });

  it('drops a flow older than the ten minutes a Google code lives', async () => {
    await pending.save(flow);

    now += 10 * 60_000;

    await expect(pending.take()).resolves.toBeNull();
    expect(native.stored.has(KEY)).toBe(false);
  });

  it.each(['{', '{"state":"s"}', '"flow"'])('drops a malformed flow (%s)', async (stored) => {
    native.stored.set(KEY, stored);

    await expect(pending.take()).resolves.toBeNull();
  });
});
