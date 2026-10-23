import type { PrerenderEpochCoverage } from '../../../../../../../core/api/generated/models/prerender-epoch-coverage';
import type { PrerenderReport } from '../../../../../../../core/api/generated/models/prerender-report';
import { prerenderReport } from '../../../../../testing/fixtures/prerender-report';
import { switchSnippets } from './switch-snippets';

const BASE = prerenderReport();
const COVERAGE = BASE.storage.epochs[0].coverage as PrerenderEpochCoverage;

function withStandby(coverage: PrerenderEpochCoverage, changes: Partial<PrerenderReport> = {}) {
  const standby = {
    epoch: 3,
    state: 'standby',
    isWriting: false,
    pages: 1_380_000,
    bytes: 53_820_000_000,
    queuePages: 0,
    queueBytes: 0,
    monthlyCost: 0.4322,
    coverage,
  };
  return prerenderReport({
    current: null,
    storage: { ...BASE.storage, epochs: [standby, ...BASE.storage.epochs] },
    ...changes,
  });
}

describe('switchSnippets', () => {
  it('gives the variable that serves an epoch ready, and what the SSR keeps', () => {
    const whole = { ...COVERAGE, gapUnits: 0, completeUnits: 8_430 };
    const snippets = switchSnippets(3, withStandby(whole));

    expect(snippets.variable).toBe('LODB_ARCHIVE_SERVE_EPOCH=3');
    expect(snippets.served).toBe(2);
    expect(snippets.state).toEqual({
      kind: 'ready',
      versions: 402,
      failedUnits: 12,
      failedPages: 270,
    });
    expect(snippets.isWritten).toBe(false);
  });

  it('keeps the retirement as text alone before the switch', () => {
    const partial = { ...COVERAGE, completeUnits: 5_420, gapUnits: 3_010 };
    const snippets = switchSnippets(3, withStandby(partial));

    expect(snippets.retire).toBeNull();
    expect(snippets.state).toEqual({ kind: 'partial', share: 5_420 / 8_442, missing: 3_022 });
  });

  it('says a pass still writes the target', () => {
    expect(switchSnippets(2, BASE).isWritten).toBe(true);
  });

  it('gives the variables of the retirement of an epoch no longer served', () => {
    expect(switchSnippets(1, BASE).retire).toEqual({
      line: 'ENVIRONMENT=prod BUCKET=lodb-pages-prod SERVED_EPOCH=2 WRITE_EPOCH=2 RETIRED_EPOCHS=1',
      canCopy: true,
      monthlyCost: 0.0019,
    });
  });

  it('writes the epoch the active pass writes, else the served one', () => {
    const writing = prerenderReport({
      servedEpoch: 3,
      current: { ...(BASE.current as NonNullable<PrerenderReport['current']>), epoch: 4 },
    });

    expect(switchSnippets(1, writing).retire?.line).toContain('SERVED_EPOCH=3 WRITE_EPOCH=4');
    expect(
      switchSnippets(1, prerenderReport({ servedEpoch: 3, current: null })).retire?.line,
    ).toContain('SERVED_EPOCH=3 WRITE_EPOCH=3');
  });

  it('keeps a placeholder, not to be copied, without the bucket', () => {
    const retire = switchSnippets(1, prerenderReport({ storeBucket: null })).retire;

    expect(retire?.line).toContain('BUCKET=<bucket>');
    expect(retire?.canCopy).toBe(false);
  });

  it('reads the environment from the bucket', () => {
    const staging = prerenderReport({ storeBucket: 'lodb-pages-staging' });
    expect(switchSnippets(1, staging).retire?.line).toMatch(/^ENVIRONMENT=staging /);
  });
});
