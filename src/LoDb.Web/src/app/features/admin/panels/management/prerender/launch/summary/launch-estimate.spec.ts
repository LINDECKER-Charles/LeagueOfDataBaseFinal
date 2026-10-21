import type { PrerenderPatchesReport } from '../../../../../../../core/api/generated/models/prerender-patches-report';
import { LOCALES } from '../../../../../../../core/i18n/locales';
import { prerenderPatches } from '../../../../../testing/fixtures/prerender-patches';
import type { LaunchDraft } from '../dialog/launch-draft';
import { launchEstimate } from './launch-estimate';

const PATCHES = prerenderPatches();
const ALL_TYPES = ['champions', 'items', 'runes', 'summoners'];
const SHARES = PATCHES.estimates.typeShares ?? { champions: 0, items: 0, runes: 0, summoners: 0 };
// 16.18.1 in the fixture: ar, cs, de, el and es hold every type; en completed with failed
// pages, holding all but the items (mask d); fr partial, holding the items only (mask 2); hu
// failed for good; the 13 others absent.
const LOCALES_OF_18 = ['ar', 'cs', 'de', 'el', 'en', 'es', 'fr', 'hu'];

function draft(changes: Partial<LaunchDraft> = {}, ticked = ['16.18.1']): LaunchDraft {
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

function patches(changes: Partial<PrerenderPatchesReport['estimates']>) {
  return prerenderPatches({ estimates: { ...PATCHES.estimates, ...changes } });
}

describe('launchEstimate', () => {
  it('renders every unit ticked when nothing is skipped', () => {
    const estimate = launchEstimate(draft({ skipComplete: false }), PATCHES);

    expect(estimate).toMatchObject({ versions: 1, locales: 21, types: 4, units: 21, skipped: 0 });
    expect(estimate.pages).toBe(21 * 1_140);
    expect(estimate.bytes).toBe(21 * 1_140 * 39_000);
    expect(estimate.rerendered).toBe(5);
  });

  it('leaves out each locale holding every type chosen, aware of the types', () => {
    // ar, cs, de, el and es hold all four: left out of a pass over every type.
    expect(launchEstimate(draft(), PATCHES)).toMatchObject({ units: 16, skipped: 5 });
    // en holds no items, fr holds them: a pass over the items leaves fr out, renders en.
    const items = launchEstimate(draft({ pageTypes: ['items'], locales: ['en', 'fr'] }), PATCHES);
    expect(items).toMatchObject({ units: 1, skipped: 1 });
    // fr holds the items only: a pass over every type renders it again.
    expect(launchEstimate(draft({ locales: ['fr'] }), PATCHES).units).toBe(1);
  });

  it('weighs restricted types by their share, or evenly without one', () => {
    const items = draft({ skipComplete: false, pageTypes: ['items', 'runes'], locales: ['ar'] });

    expect(launchEstimate(items, PATCHES).share).toBeCloseTo(0.5803 + 0.1894);
    expect(launchEstimate(items, PATCHES).pages).toBe(Math.round(1_140 * (0.5803 + 0.1894)));
    expect(launchEstimate(items, patches({ typeShares: null })).share).toBe(0.5);
  });

  it('assumes the pace measured, never above the configured ceiling', () => {
    const measured = launchEstimate(draft(), PATCHES);
    expect(measured).toMatchObject({ pace: 112.6, paceSource: 'measured' });
    expect(measured.seconds).toBeCloseTo((measured.pages / 112.6) * 60);

    expect(launchEstimate(draft(), patches({ configuredRendersPerMinute: 30 }))).toMatchObject({
      pace: 30,
      paceSource: 'capped',
    });
    expect(launchEstimate(draft(), patches({ measuredPagesPerMinute: null }))).toMatchObject({
      pace: 120,
      paceSource: 'ceiling',
    });
  });

  it('costs only the pages not stored yet, cell by cell, by the types each holds', () => {
    const { champions, items, runes, summoners } = SHARES;
    // ar, cs, de, el, es hold all four: nothing added. en holds all but the items, fr the
    // items only: each adds the others; hu failed and the 13 absent hold none: whole.
    const added = Math.round(1_140 * (items + (champions + runes + summoners) + 1 + 13));
    const served = launchEstimate(draft({ skipComplete: false }), PATCHES);
    expect(served.addedBytes).toBe(added * 39_000);
    expect(served.addedCost).toBeCloseTo(((added * 39_000) / 1e9) * 0.00803);
  });

  it('adds the pages of a failed locale, though its patch stores others', () => {
    // 16.18.1 stores 6 140 pages, none of them in hu, failed for good.
    const failed = launchEstimate(draft({ skipComplete: false, locales: ['hu'] }), PATCHES);

    expect(failed.addedBytes).toBe(1_140 * 39_000);
  });

  it('counts every unit of a new epoch as new, none held', () => {
    const fresh = launchEstimate(draft({ epoch: 3, epochKind: 'new' }), PATCHES);

    expect(fresh).toMatchObject({ units: 21, skipped: 0, rerendered: 0 });
    expect(fresh.addedBytes).toBe(fresh.bytes);
  });

  it('says which versions were measured and what stood in for the others', () => {
    const unmeasured = prerenderPatches({
      patches: [PATCHES.patches[0], { ...PATCHES.patches[1], pagesPerLocale: null }],
    });
    const estimate = launchEstimate(draft({}, ['16.19.1', '16.18.1']), unmeasured);

    expect(estimate.measured).toBe(1);
    expect(estimate.guesses).toEqual([
      { version: '16.18.1', pages: 1_158, source: 'neighbour', from: '16.19.1' },
    ]);
    expect(estimate.isDefault).toBe(false);
  });

  it('marks an estimate without any measure as the default one', () => {
    const none = prerenderPatches({
      patches: PATCHES.patches.map((row) => ({ ...row, pagesPerLocale: null })),
    });
    const estimate = launchEstimate(draft({ locales: LOCALES_OF_18 }), none);

    expect(estimate.isDefault).toBe(true);
    expect(estimate.pages).toBe(estimate.units * 500);
  });
});
