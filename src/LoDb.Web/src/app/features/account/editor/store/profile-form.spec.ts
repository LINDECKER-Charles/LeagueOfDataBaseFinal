import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import type { FavoriteView } from '../../../../core/api/generated/models/favorite-view';
import type { OwnerProfile } from '../../../../core/api/generated/models/owner-profile';
import { ProfileAutosave } from '../autosave/profile-autosave';
import { ProfileForm } from './profile-form';

const PRESENT = { status: 'present', url: '/cdn/blobs/aatrox.png' } as const;
const EMPTY: FavoriteView = { current: null, status: 'empty', storedId: null };

/** Aatrox resolved, an item the patch lacks, the other slots empty, a private profile. */
function ownerProfile(): OwnerProfile {
  return {
    backdrop: null,
    deletionConfirmation: 'password',
    hasPassword: true,
    isPublic: false,
    maskedEmail: 'f***@example.com',
    memberSince: '2026-01-02T00:00:00Z',
    preferredVersion: null,
    riotTagline: null,
    username: 'faker',
    showcase: {
      isCatalogAvailable: true,
      language: 'en_US',
      version: '16.14.1',
      skin: {
        banner: '/b.jpg',
        championId: 'Ahri',
        id: 'Ahri_7',
        name: 'Arcade Ahri',
        number: 7,
        splash: '/s.jpg',
      },
      favorites: {
        champion: {
          current: { id: 'Aatrox', name: 'Aatrox', image: PRESENT },
          status: 'resolved',
          storedId: 'Aatrox',
        },
        item: { current: null, status: 'unavailable', storedId: '999999' },
        rune: EMPTY,
        summoner: EMPTY,
      },
    },
  };
}

const FLASH = { id: 'SummonerFlash', name: 'Flash', image: null, searchText: 'flash' };

function setUp() {
  TestBed.configureTestingModule({
    providers: [provideHttpClient(), provideHttpClientTesting(), ProfileAutosave, ProfileForm],
  });
  const form = TestBed.inject(ProfileForm);
  form.load(ownerProfile());
  return {
    form,
    autosave: TestBed.inject(ProfileAutosave),
    http: TestBed.inject(HttpTestingController),
  };
}

describe('ProfileForm', () => {
  beforeEach(() => {
    vi.useFakeTimers();
  });

  afterEach(() => {
    TestBed.inject(HttpTestingController).verify();
    vi.useRealTimers();
  });

  it('shows the favorites as resolved, and a favorite the patch lacks by its stored id', () => {
    const { form } = setUp();

    expect(form.favorites().champion).toEqual({
      id: 'Aatrox',
      name: 'Aatrox',
      image: '/cdn/blobs/aatrox.png',
    });
    expect(form.favorites().item).toEqual({ id: '999999', name: null, image: null });
    expect(form.skin()).toEqual({ id: 'Ahri_7', name: 'Arcade Ahri', banner: '/b.jpg' });
  });

  it('shows a pick at once, then saves every slot after a pause, on the resolved version', async () => {
    const { form, autosave, http } = setUp();

    form.pick('summoner', FLASH);
    expect(form.favorites().summoner.name).toBe('Flash');
    http.expectNone('/api/profile/favorites?version=16.14.1');
    await vi.advanceTimersByTimeAsync(600);

    const request = http.expectOne('/api/profile/favorites?version=16.14.1');
    expect(request.request.method).toBe('PUT');
    expect(request.request.body).toEqual({
      champion: 'Aatrox',
      item: '999999',
      rune: null,
      summoner: 'SummonerFlash',
      skin: 'Ahri_7',
    });
    request.flush({
      favorites: {},
      isSkinRejected: false,
      rejected: [],
      skin: 'Ahri_7',
    });
    await vi.advanceTimersByTimeAsync(0);

    expect(autosave.status()).toBe('saved');
  });

  it('gathers a burst of picks into one save', async () => {
    const { form, http } = setUp();

    form.pick('summoner', FLASH);
    await vi.advanceTimersByTimeAsync(300);
    form.pick('champion', null);
    form.pickSkin(null);
    await vi.advanceTimersByTimeAsync(600);

    const request = http.expectOne('/api/profile/favorites?version=16.14.1');
    expect(request.request.body).toMatchObject({
      champion: null,
      summoner: 'SummonerFlash',
      skin: null,
    });
    request.flush({ favorites: {}, isSkinRejected: false, rejected: [], skin: null });
  });

  it('empties the slots the server rejected and warns, the status staying on screen', async () => {
    const { form, autosave, http } = setUp();

    form.pick('summoner', FLASH);
    await vi.advanceTimersByTimeAsync(600);
    http.expectOne('/api/profile/favorites?version=16.14.1').flush({
      favorites: {},
      isSkinRejected: true,
      rejected: ['summoner'],
      skin: null,
    });
    await vi.advanceTimersByTimeAsync(3000);

    expect(form.favorites().summoner).toEqual({ id: null, name: null, image: null });
    expect(form.skin()).toBeNull();
    expect(autosave.status()).toBe('warned');
  });

  it('keeps a slot picked again while its save was on its way', async () => {
    const { form, http } = setUp();
    const ghost = { ...FLASH, id: 'SummonerHaste', name: 'Ghost' };

    form.pick('summoner', FLASH);
    await vi.advanceTimersByTimeAsync(600);
    const request = http.expectOne('/api/profile/favorites?version=16.14.1');
    form.pick('summoner', ghost);
    request.flush({ favorites: {}, isSkinRejected: false, rejected: ['summoner'], skin: 'Ahri_7' });
    await vi.advanceTimersByTimeAsync(0);

    expect(form.favorites().summoner.id).toBe('SummonerHaste');
    await vi.advanceTimersByTimeAsync(600);
    http
      .expectOne('/api/profile/favorites?version=16.14.1')
      .flush({ favorites: {}, isSkinRejected: false, rejected: [], skin: 'Ahri_7' });
  });

  it('flips the visibility at once and saves it after a pause', async () => {
    const { form, autosave, http } = setUp();

    form.setPublic(true);
    expect(form.isPublic()).toBe(true);
    await vi.advanceTimersByTimeAsync(600);

    const request = http.expectOne('/api/profile/visibility');
    expect(request.request.method).toBe('PUT');
    expect(request.request.body).toEqual({ isPublic: true });
    request.flush('');
    await vi.advanceTimersByTimeAsync(0);

    expect(autosave.status()).toBe('saved');
  });

  it('says the save failed, and keeps what was picked', async () => {
    const { form, autosave, http } = setUp();

    form.pick('summoner', FLASH);
    await vi.advanceTimersByTimeAsync(600);
    http
      .expectOne('/api/profile/favorites?version=16.14.1')
      .flush(null, { status: 503, statusText: 'Service Unavailable' });
    await vi.advanceTimersByTimeAsync(0);

    expect(autosave.status()).toBe('error');
    expect(form.favorites().summoner.id).toBe('SummonerFlash');
  });
});
