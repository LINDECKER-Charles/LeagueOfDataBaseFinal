import { LOCALES } from '../../../../../../../core/i18n/locales';
import { prerenderPatches } from '../../../../../testing/fixtures/prerender-patches';
import type { LaunchDraft } from '../dialog/launch-draft';
import { requestOfDraft } from './request-of-draft';

const PATCHES = prerenderPatches();
const ALL_TYPES = ['champions', 'items', 'runes', 'summoners'];

function draft(changes: Partial<LaunchDraft> = {}, ticked = ['16.19.1', '16.18.1']): LaunchDraft {
  return {
    origin: 'header',
    selection: { selected: new Set(ticked), anchor: null, priority: [], extent: null },
    locales: LOCALES,
    pageTypes: ALL_TYPES,
    epoch: 2,
    epochKind: 'served',
    skipComplete: true,
    fromPass: null,
    isResolved: true,
    isPriorityCapped: false,
    ...changes,
  };
}

describe('requestOfDraft', () => {
  it('sends a whole pass as nulls, its epoch explicit', () => {
    expect(requestOfDraft(draft(), PATCHES)).toEqual({
      epoch: 2,
      versions: null,
      locales: null,
      pageTypes: null,
      priority: null,
      skipComplete: true,
    });
  });

  it('lists the versions newest first when some only are ticked', () => {
    const one = requestOfDraft(draft({}, ['16.18.1']), PATCHES);

    expect(one.versions).toEqual(['16.18.1']);
  });

  it('lists the locales in the site order and the types in the API order', () => {
    const request = requestOfDraft(
      draft({ locales: ['zh-hans', 'fr', 'en'], pageTypes: ['summoners', 'items'] }),
      PATCHES,
    );

    expect(request.locales).toEqual(['en', 'fr', 'zh-hans']);
    expect(request.pageTypes).toEqual(['items', 'summoners']);
  });

  it('keeps the priorities in their order', () => {
    const selection = {
      selected: new Set(['16.19.1', '16.18.1']),
      anchor: null,
      priority: ['16.18.1', '16.19.1'],
      extent: null,
    };

    expect(requestOfDraft(draft({ selection }), PATCHES).priority).toEqual(['16.18.1', '16.19.1']);
  });

  it('never skips anything on a new epoch, which holds nothing', () => {
    const fresh = requestOfDraft(draft({ epoch: 4, epochKind: 'new' }), PATCHES);

    expect(fresh).toMatchObject({ epoch: 4, skipComplete: false });
    expect(requestOfDraft(draft({ epochKind: 'standby', epoch: 3 }), PATCHES).skipComplete).toBe(
      true,
    );
  });
});
