import { TestBed } from '@angular/core/testing';
import type { ProfileSave } from './profile-save';
import { ProfileAutosave } from './profile-autosave';

function autosave(): ProfileAutosave {
  TestBed.configureTestingModule({ providers: [ProfileAutosave] });
  return TestBed.inject(ProfileAutosave);
}

function answering(outcome: 'saved' | 'warned') {
  return vi.fn<ProfileSave>(async () => outcome);
}

describe('ProfileAutosave', () => {
  beforeEach(() => {
    vi.useFakeTimers();
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it('waits for a pause, then sends the last change of a part once, and says saved', async () => {
    const saves = autosave();
    const first = answering('saved');
    const last = answering('saved');

    saves.schedule('favorites', first);
    await vi.advanceTimersByTimeAsync(300);
    saves.schedule('favorites', last);
    await vi.advanceTimersByTimeAsync(300);
    expect(last).not.toHaveBeenCalled();
    await vi.advanceTimersByTimeAsync(300);

    expect(first).not.toHaveBeenCalled();
    expect(last).toHaveBeenCalledOnce();
    expect(saves.status()).toBe('saved');
  });

  it('says saving while the request is on its way', async () => {
    const saves = autosave();
    let answer!: (outcome: 'saved') => void;
    saves.schedule('visibility', () => new Promise((resolve) => (answer = resolve)));

    await vi.advanceTimersByTimeAsync(600);
    expect(saves.status()).toBe('saving');
    answer('saved');
    await vi.advanceTimersByTimeAsync(0);

    expect(saves.status()).toBe('saved');
  });

  it('fades a clean save back to idle', async () => {
    const saves = autosave();
    saves.schedule('favorites', answering('saved'));

    await vi.advanceTimersByTimeAsync(600);
    await vi.advanceTimersByTimeAsync(3000);

    expect(saves.status()).toBe('idle');
  });

  it('keeps a warning on screen past the idle delay', async () => {
    const saves = autosave();
    saves.schedule('favorites', answering('warned'));

    await vi.advanceTimersByTimeAsync(600);
    await vi.advanceTimersByTimeAsync(3000);

    expect(saves.status()).toBe('warned');
  });

  it('sends the parts changed together in one run, the worst outcome told', async () => {
    const saves = autosave();
    const favorites = answering('warned');
    const visibility = answering('saved');

    saves.schedule('favorites', favorites);
    saves.schedule('visibility', visibility);
    await vi.advanceTimersByTimeAsync(600);

    expect(favorites).toHaveBeenCalledOnce();
    expect(visibility).toHaveBeenCalledOnce();
    expect(saves.status()).toBe('warned');
  });

  it('keeps a failure on screen, and retry sends the failed save again', async () => {
    const saves = autosave();
    const save = vi
      .fn<ProfileSave>()
      .mockRejectedValueOnce(new Error('offline'))
      .mockResolvedValueOnce('saved');
    saves.schedule('favorites', save);

    await vi.advanceTimersByTimeAsync(3600);
    expect(saves.status()).toBe('error');
    await saves.retry();

    expect(save).toHaveBeenCalledTimes(2);
    expect(saves.status()).toBe('saved');
  });

  it('drops a failed save a newer change of the same part replaces', async () => {
    const saves = autosave();
    const failing = vi.fn<ProfileSave>().mockRejectedValue(new Error('offline'));
    const newer = answering('saved');
    saves.schedule('favorites', failing);
    await vi.advanceTimersByTimeAsync(600);

    saves.schedule('favorites', newer);
    await vi.advanceTimersByTimeAsync(600);
    await saves.retry();

    expect(failing).toHaveBeenCalledOnce();
    expect(newer).toHaveBeenCalledOnce();
  });

  it('never runs two saves at once', async () => {
    const saves = autosave();
    const order: string[] = [];
    let answer!: (outcome: 'saved') => void;
    saves.schedule('visibility', () => {
      order.push('visibility');
      return new Promise((resolve) => (answer = resolve));
    });
    await vi.advanceTimersByTimeAsync(600);

    saves.schedule('favorites', async () => {
      order.push('favorites');
      return 'saved';
    });
    await vi.advanceTimersByTimeAsync(600);
    expect(order).toEqual(['visibility']);
    answer('saved');
    await vi.advanceTimersByTimeAsync(0);

    expect(order).toEqual(['visibility', 'favorites']);
  });

  it('sends what still waits when the editor goes away', async () => {
    const saves = autosave();
    const save = answering('saved');
    saves.schedule('favorites', save);

    TestBed.resetTestingModule();
    await vi.advanceTimersByTimeAsync(0);

    expect(save).toHaveBeenCalledOnce();
  });
});
