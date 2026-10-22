import { LOCALES } from '../../../../../../../core/i18n/locales';
import { prerenderPatches } from '../../../../../testing/fixtures/prerender-patches';
import { prerenderReport } from '../../../../../testing/fixtures/prerender-report';
import type { LaunchPreset } from '../../common/scope/launch-preset';
import { draftOf } from './launch-draft';

const REPORT = prerenderReport();
const PATCHES = prerenderPatches();
const HEADER: LaunchPreset = {
  origin: 'header',
  versions: [],
  priority: [],
  locales: null,
  pageTypes: null,
  epoch: null,
  skipComplete: true,
  fromPass: null,
};

function ticked(preset: LaunchPreset): string[] {
  return [...draftOf(preset, REPORT, PATCHES).selection.selected];
}

describe('draftOf', () => {
  it('opens the header on nothing ticked, every locale and type of the served epoch', () => {
    const draft = draftOf(HEADER, REPORT, PATCHES);

    expect(draft.selection.selected.size).toBe(0);
    expect(draft.locales).toEqual(LOCALES);
    expect(draft.pageTypes).toEqual(['champions', 'items', 'runes', 'summoners']);
    expect(draft).toMatchObject({ epoch: 2, epochKind: 'served', skipComplete: true });
    expect(draft).toMatchObject({ isResolved: true, origin: 'header', fromPass: null });
    expect(draft.openersFirst).toBe(false);
  });

  it('opens on the openers alone when the preset rendered them so', () => {
    expect(draftOf({ ...HEADER, pages: 'openers' }, REPORT, PATCHES).openersFirst).toBe(true);
  });

  it('waits for the coverage before ticking anything', () => {
    const draft = draftOf({ ...HEADER, origin: 'idle', versions: 'incomplete' }, REPORT, null);

    expect(draft.isResolved).toBe(false);
    expect(draft.selection.selected.size).toBe(0);
  });

  it('ticks every older patch for all, the ones with a gap for incomplete', () => {
    expect(ticked({ ...HEADER, versions: 'all' })).toEqual(['16.19.1', '16.18.1']);
    expect(ticked({ ...HEADER, origin: 'idle', versions: 'incomplete' })).toEqual(['16.18.1']);
    expect(ticked({ ...HEADER, origin: 'complete-epoch', versions: 'incomplete' })).toEqual([
      '16.18.1',
    ]);
  });

  it('leaves a failed cell to the relaunch: incomplete takes - and p only', () => {
    const failedOnly = prerenderPatches({
      patches: [{ ...PATCHES.patches[1], cells: 'cccccccccccccccccccff' }],
    });
    const preset: LaunchPreset = { ...HEADER, origin: 'idle', versions: 'incomplete' };

    expect(draftOf(preset, REPORT, failedOnly).selection.selected.size).toBe(0);
  });

  it('keeps the coverage selection of the coverage tab, on the epoch it read', () => {
    const preset: LaunchPreset = {
      ...HEADER,
      origin: 'coverage',
      versions: ['16.18.1', '3.15.9'],
      epoch: 3,
    };
    const draft = draftOf(preset, REPORT, PATCHES);

    expect([...draft.selection.selected]).toEqual(['16.18.1']);
    expect(draft).toMatchObject({ epoch: 3, epochKind: 'new' });
  });

  it('keeps the perimeter of a relaunch and only the priorities ticked', () => {
    const preset: LaunchPreset = {
      origin: 'relaunch',
      versions: ['16.18.1'],
      priority: ['16.19.1', '16.18.1'],
      locales: ['en', 'fr'],
      pageTypes: ['items'],
      epoch: 2,
      skipComplete: false,
      fromPass: 11,
    };
    const draft = draftOf(preset, REPORT, PATCHES);

    expect(draft.selection.priority).toEqual(['16.18.1']);
    expect(draft).toMatchObject({ locales: ['en', 'fr'], pageTypes: ['items'], fromPass: 11 });
    expect(draft.skipComplete).toBe(false);
  });

  it('relaunches the failures of a pass with its locales and types, missing ones only', () => {
    const preset: LaunchPreset = {
      ...HEADER,
      origin: 'failures',
      versions: ['16.18.1'],
      locales: ['de'],
      pageTypes: ['champions', 'items'],
      fromPass: 12,
    };

    expect(draftOf(preset, REPORT, PATCHES)).toMatchObject({
      origin: 'failures',
      locales: ['de'],
      pageTypes: ['champions', 'items'],
      skipComplete: true,
      fromPass: 12,
    });
  });
});
