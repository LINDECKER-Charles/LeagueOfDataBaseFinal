/** What the audience reads of the document, settable per test; its events are dispatched. */
export interface FakeAudienceDocument extends EventTarget {
  location: { hostname: string };
  referrer: string;
  title: string;
  visibilityState: DocumentVisibilityState;
  defaultView: {
    navigator: { language: string; globalPrivacyControl?: boolean; webdriver?: boolean };
    screen: { width: number; height: number };
    localStorage: Storage;
  };
}

/** A visible document of the canonical host, in French, on a 1920 x 1080 screen, nothing stored. */
export function fakeAudienceDocument(): FakeAudienceDocument {
  const stored = new Map<string, string>();
  const localStorage = {
    getItem: (key: string) => stored.get(key) ?? null,
    setItem: (key: string, value: string) => void stored.set(key, value),
    removeItem: (key: string) => void stored.delete(key),
  } as Storage;
  return Object.assign(new EventTarget(), {
    location: { hostname: 'league-of-data-base.com' },
    referrer: '',
    title: 'League Of Data Base',
    visibilityState: 'visible' as DocumentVisibilityState,
    defaultView: {
      navigator: { language: 'fr-FR' },
      screen: { width: 1920, height: 1080 },
      localStorage,
    },
  });
}
