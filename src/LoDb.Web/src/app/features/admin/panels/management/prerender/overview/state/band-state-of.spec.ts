import type { PrerenderEpochCoverage } from '../../../../../../../core/api/generated/models/prerender-epoch-coverage';
import type { PrerenderEpochVolume } from '../../../../../../../core/api/generated/models/prerender-epoch-volume';
import type { PrerenderReport } from '../../../../../../../core/api/generated/models/prerender-report';
import { prerenderReport } from '../../../../../testing/fixtures/prerender-report';
import { bandStateOf } from './band-state-of';

const BASE = prerenderReport();
const COVERAGE = BASE.storage.epochs[0].coverage as PrerenderEpochCoverage;
const WHOLE: PrerenderEpochCoverage = {
  ...COVERAGE,
  completeUnits: 8_430,
  cleanUnits: 8_400,
  failedUnits: 12,
  gapUnits: 0,
  gapVersions: 0,
};

function standby(coverage: PrerenderEpochCoverage, changes: Partial<PrerenderEpochVolume> = {}) {
  return {
    epoch: 3,
    state: 'standby',
    isWriting: false,
    pages: 1_380_000,
    bytes: 53_820_000_000,
    monthlyCost: 0.4322,
    coverage,
    ...changes,
  };
}

function withEpoch(volume: PrerenderEpochVolume, changes: Partial<PrerenderReport> = {}) {
  return prerenderReport({
    current: null,
    storage: { ...BASE.storage, epochs: [volume, ...BASE.storage.epochs] },
    ...changes,
  });
}

// 90 % of 8,442 units: 844 gaps at most.
function gaps(count: number): PrerenderEpochCoverage {
  return { ...COVERAGE, completeUnits: 8_442 - count, failedUnits: 0, gapUnits: count };
}

describe('bandStateOf', () => {
  it('says the host is not configured', () => {
    expect(bandStateOf(prerenderReport({ isConfigured: false }))).toEqual({
      isDormant: true,
      epoch: null,
    });
  });

  it('says an epoch is ready, its failures for good and failed pages named', () => {
    expect(bandStateOf(withEpoch(standby(WHOLE), { readyEpoch: 3 })).epoch).toEqual({
      kind: 'ready',
      epoch: 3,
      versions: 402,
      failedUnits: 12,
      failedPages: 270,
    });
  });

  it('counts the older patches from another epoch when the ready one has no coverage', () => {
    const band = bandStateOf(withEpoch(standby(WHOLE, { coverage: null }), { readyEpoch: 3 }));

    expect(band.epoch).toEqual({
      kind: 'ready',
      epoch: 3,
      versions: 402,
      failedUnits: 0,
      failedPages: 0,
    });
  });

  it('says a standby epoch nearly whole is worth completing', () => {
    expect(bandStateOf(withEpoch(standby(gaps(844)))).epoch).toEqual({
      kind: 'nearly',
      epoch: 3,
      share: (8_442 - 844) / 8_442,
      missing: 844,
    });
  });

  it('says nothing of an epoch below 90 %, or one a pass still writes', () => {
    expect(bandStateOf(withEpoch(standby(gaps(845)))).epoch).toBeNull();
    expect(bandStateOf(withEpoch(standby(gaps(10), { isWriting: true }))).epoch).toBeNull();
  });

  it('lets the ready epoch win over one nearly ready', () => {
    const nearly = standby(gaps(10), { epoch: 4 });
    const report = withEpoch(nearly, { readyEpoch: 2 });

    expect(bandStateOf(report).epoch?.kind).toBe('ready');
    expect(bandStateOf(report).epoch?.epoch).toBe(2);
  });

  it('has no band when all is ordinary', () => {
    expect(bandStateOf(BASE)).toEqual({ isDormant: false, epoch: null });
  });
});
