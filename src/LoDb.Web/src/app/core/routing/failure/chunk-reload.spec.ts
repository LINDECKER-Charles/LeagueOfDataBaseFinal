import { LocationStrategy } from '@angular/common';
import { DOCUMENT, Injector, PLATFORM_ID } from '@angular/core';
import { ChunkReload } from './chunk-reload';

// The home of a locale, as LocaleHomeLocationStrategy writes it.
const HOME = '/fr';
const CHAMPIONS = '/fr/champions?tags=Mage';

function memoryStorage(): Storage {
  const items = new Map<string, string>();
  return {
    getItem: (key: string) => items.get(key) ?? null,
    setItem: (key: string, value: string) => items.set(key, value),
    removeItem: (key: string) => items.delete(key),
  } as unknown as Storage;
}

function refusingStorage(): Storage {
  const refuse = () => {
    throw new DOMException('The quota has been exceeded.', 'QuotaExceededError');
  };
  return { getItem: () => null, setItem: refuse, removeItem: refuse } as unknown as Storage;
}

// A tab whose location only records where it was sent.
function tab(storage: Storage | null = memoryStorage(), platform = 'browser') {
  const assign = vi.fn();
  const document = { location: { assign }, defaultView: { sessionStorage: storage } };
  const injector = Injector.create({
    providers: [
      { provide: DOCUMENT, useValue: document },
      { provide: PLATFORM_ID, useValue: platform },
      { provide: LocationStrategy, useValue: { prepareExternalUrl: (url: string) => `${url}/` } },
      { provide: ChunkReload },
    ],
  });
  return { reload: () => injector.get(ChunkReload), assign };
}

describe('ChunkReload', () => {
  it('loads the page again at its external address', () => {
    const { reload, assign } = tab();

    expect(reload().reloadOnce(HOME)).toBe(true);
    expect(assign).toHaveBeenCalledExactlyOnceWith('/fr/');
  });

  it('gives up when the reload of the same address fails in its turn', () => {
    const storage = memoryStorage();
    const before = tab(storage);
    const after = tab(storage);

    expect(before.reload().reloadOnce(CHAMPIONS)).toBe(true);
    expect(after.reload().reloadOnce(CHAMPIONS)).toBe(false);
    expect(after.assign).not.toHaveBeenCalled();
  });

  it('reloads again for a later attempt, once the failure was shown', () => {
    const storage = memoryStorage();
    const { reload, assign } = tab(storage);

    reload().reloadOnce(CHAMPIONS);
    reload().reloadOnce(CHAMPIONS);

    expect(reload().reloadOnce(CHAMPIONS)).toBe(true);
    expect(assign).toHaveBeenCalledTimes(2);
  });

  it('reloads for another address', () => {
    const { reload, assign } = tab();

    reload().reloadOnce(CHAMPIONS);

    expect(reload().reloadOnce(HOME)).toBe(true);
    expect(assign).toHaveBeenLastCalledWith('/fr/');
  });

  it.each([
    ['the server', memoryStorage(), 'server'],
    ['a tab without storage', null, 'browser'],
    ['a tab that refuses storage', refusingStorage(), 'browser'],
  ])('never reloads on %s', (_case, storage, platform) => {
    const { reload, assign } = tab(storage, platform);

    expect(reload().reloadOnce(HOME)).toBe(false);
    expect(assign).not.toHaveBeenCalled();
  });
});
