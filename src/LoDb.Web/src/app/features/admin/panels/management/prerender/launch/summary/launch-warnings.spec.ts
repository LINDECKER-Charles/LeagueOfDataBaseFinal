import type { PrerenderEpochVolume } from '../../../../../../../core/api/generated/models/prerender-epoch-volume';
import { LOCALES } from '../../../../../../../core/i18n/locales';
import { prerenderPatches } from '../../../../../testing/fixtures/prerender-patches';
import { prerenderReport } from '../../../../../testing/fixtures/prerender-report';
import type { LaunchDraft } from '../dialog/launch-draft';
import { type LaunchEstimate, launchEstimate } from './launch-estimate';
import { launchWarnings } from './launch-warnings';

const REPORT = prerenderReport();
const DRAFT: LaunchDraft = {
  origin: 'header',
  selection: { selected: new Set(['16.18.1']), anchor: null, priority: [], extent: null },
  locales: LOCALES,
  pageTypes: ['champions', 'items', 'runes', 'summoners'],
  epoch: 2,
  epochKind: 'served',
  skipComplete: true,
  isOpenersFirst: false,
  fromPass: null,
  isResolved: true,
  isPriorityCapped: false,
};
const ESTIMATE = launchEstimate(DRAFT, prerenderPatches());

function codes(
  draft: Partial<LaunchDraft> = {},
  estimate: Partial<LaunchEstimate> = {},
  report = REPORT,
): string[] {
  const warnings = launchWarnings({ ...DRAFT, ...draft }, { ...ESTIMATE, ...estimate }, report);
  return warnings.map((warning) => warning.code);
}

describe('launchWarnings', () => {
  it('says nothing of a plain pass on the served epoch', () => {
    expect(codes()).toEqual([]);
  });

  it('refuses a pass with nothing to render, in red', () => {
    const [nothing] = launchWarnings(DRAFT, { ...ESTIMATE, units: 0 }, REPORT);

    expect(nothing).toEqual({ code: 'nothing', params: { epoch: 2 }, tone: 'bad' });
  });

  it('warns that units already complete are rendered again', () => {
    const [rerender] = launchWarnings(
      { ...DRAFT, skipComplete: false },
      { ...ESTIMATE, rerendered: 5 },
      REPORT,
    );

    expect(rerender.params).toEqual({ units: 5 });
    expect(codes({ skipComplete: false }, { rerendered: 0 })).toEqual([]);
  });

  it('warns of a long pass, by its duration or its pages', () => {
    expect(codes({}, { seconds: 3 * 86_400 + 1 })).toEqual(['long']);
    expect(codes({}, { pages: 1_000_001 })).toEqual(['long']);
    const [long] = launchWarnings(DRAFT, { ...ESTIMATE, seconds: 6 * 86_400 }, REPORT);
    expect(long.params).toEqual({ duration: '6 j 0 h' });
  });

  it('warns that a pass kept to some types takes no promotion', () => {
    expect(codes({ pageTypes: ['items'] })).toEqual(['types']);
    expect(codes({ pageTypes: ['items'] }, {}, { ...REPORT, enqueuesPromotions: false })).toEqual(
      [],
    );
  });

  it('warns that the openers leave every other page to a pass that must follow', () => {
    expect(codes({ isOpenersFirst: true })).toEqual(['openers']);
    expect(codes({ isOpenersFirst: true, pageTypes: ['items'] })).toEqual(['openers']);
  });

  it('warns that nothing of a new or standby epoch is served before the switch', () => {
    expect(codes({ epoch: 3, epochKind: 'standby' })).toEqual(['new_epoch']);
    expect(codes({ epoch: 3, epochKind: 'new' })).toEqual(['new_epoch']);
  });

  it('points a new epoch to the standby one that exists', () => {
    const standby: PrerenderEpochVolume = {
      ...REPORT.storage.epochs[0],
      epoch: 3,
      state: 'standby',
    };
    const report = {
      ...REPORT,
      storage: { ...REPORT.storage, epochs: [standby, ...REPORT.storage.epochs] },
    };

    expect(codes({ epoch: 4, epochKind: 'new' }, {}, report)).toEqual([
      'new_epoch',
      'standby_exists',
    ]);
  });

  it('warns of an estimate without measure and of priorities past 100', () => {
    expect(codes({}, { isDefault: true })).toEqual(['default_estimate']);
    expect(codes({ isPriorityCapped: true })).toEqual(['priority_capped']);
  });
});
