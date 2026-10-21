import type { PrerenderPassProgress } from '../../../../../../core/api/generated/models/prerender-pass-progress';
import type { PrerenderVersionProgress } from '../../../../../../core/api/generated/models/prerender-version-progress';
import { prerenderProgress } from '../../../../testing/fixtures/prerender-progress';
import { mergeProgress } from './merge-progress';

const BASE = prerenderProgress();
const FIRST_AT = Date.parse(BASE.generatedAt);
const OLDER = { ...BASE.versions[0], version: '15.2.1', rank: 2, cells: 'c'.repeat(21) };
const PRIORITY = { ...BASE.versions[0], version: '16.12.1', rank: 1, cells: '.'.repeat(21) };

function version(name: string, changes: Partial<PrerenderVersionProgress> = {}) {
  return { ...BASE.versions[0], version: name, ...changes };
}

// A complete answer, every version of the pass and the list of its scope.
function complete(changes: Partial<PrerenderPassProgress> = {}): PrerenderPassProgress {
  return prerenderProgress({
    isComplete: true,
    versions: [BASE.versions[0], PRIORITY, OLDER],
    scopeVersions: ['16.18.1', '16.12.1', '15.2.1'],
    ...changes,
  });
}

// An incremental answer `seconds` after the first.
function later(seconds: number, changes: Partial<PrerenderPassProgress> = {}) {
  return prerenderProgress({
    generatedAt: new Date(FIRST_AT + seconds * 1_000).toISOString(),
    ...changes,
  });
}

function versions(progress: PrerenderPassProgress): string[] {
  return progress.versions.map((item) => `${item.version}:${item.cells.slice(0, 7)}`);
}

describe('mergeProgress', () => {
  it('takes a complete answer whole', () => {
    const state = mergeProgress(undefined, complete(), 1_000);

    expect(versions(state.progress)).toEqual([
      '16.18.1:ccwcccr',
      '16.12.1:.......',
      '15.2.1:ccccccc',
    ]);
    expect(state.receivedAt).toBe(1_000);
    expect(state.completeAt).toBe(1_000);
    expect(state.events.map((event) => event.kind)).toEqual(['done']);
  });

  it('updates the versions an incremental answer carries, and keeps the others', () => {
    const first = mergeProgress(undefined, complete(), 1_000);
    const changed = version('16.18.1', { cells: 'ccwccccr.............' });
    const state = mergeProgress(first, later(5, { versions: [changed] }), 6_000);

    expect(versions(state.progress)).toEqual([
      '16.18.1:ccwcccc',
      '16.12.1:.......',
      '15.2.1:ccccccc',
    ]);
    expect(state.completeAt).toBe(1_000);
  });

  it('adds a version that joined, in the order of its rank', () => {
    const first = mergeProgress(undefined, complete(), 1_000);
    const joined = version('16.20.1', { rank: 1, cells: '.'.repeat(21) });
    const state = mergeProgress(first, later(5, { versions: [joined] }), 6_000);

    expect(state.progress.versions.map((item) => item.version)).toEqual([
      '16.18.1',
      '16.12.1',
      '16.20.1',
      '15.2.1',
    ]);
    expect(state.events[0]).toMatchObject({ kind: 'joined', version: '16.20.1' });
  });

  it('keeps the scope list from one complete answer to the next', () => {
    const first = mergeProgress(undefined, complete(), 1_000);
    const state = mergeProgress(first, later(5), 6_000);

    expect(state.progress.scopeVersions).toEqual(['16.18.1', '16.12.1', '15.2.1']);
    const again = mergeProgress(state, complete({ scopeVersions: ['16.18.1'] }), 7_000);
    expect(again.progress.scopeVersions).toEqual(['16.18.1']);
  });

  it('never takes the share on screen back when the total grows, then catches up', () => {
    const first = mergeProgress(undefined, complete(), 1_000);
    const left = (BASE.pass.pagesLeft ?? 0) + 300_000;
    const grown = mergeProgress(
      first,
      later(5, { pass: { ...BASE.pass, pagesLeft: left } }),
      6_000,
    );

    expect(grown.shownShare).toBeCloseTo(first.shownShare);
    expect(grown.events[0]?.kind).toBe('total_up');

    const written = BASE.pass.pagesWritten + 600_000;
    const pass = { ...BASE.pass, pagesWritten: written, pagesLeft: left - 600_000 };
    const caught = mergeProgress(grown, later(10, { pass }), 11_000);
    expect(caught.shownShare).toBeGreaterThan(first.shownShare);
  });

  it('starts over for another pass', () => {
    const first = mergeProgress(undefined, complete(), 1_000);
    const pass = { ...BASE.pass, id: 13, pagesWritten: 10, pagesSkipped: 0, pagesFailed: 0 };
    const state = mergeProgress(first, complete({ pass, versions: [OLDER] }), 6_000);

    expect(state.shownShare).toBeLessThan(0.01);
    expect(versions(state.progress)).toEqual(['15.2.1:ccccccc']);
    expect(state.samples.samples).toHaveLength(1);
  });

  it('keeps 50 lines of activity at most', () => {
    let state = mergeProgress(undefined, complete(), 1_000);
    for (let index = 1; index <= 60; index++) {
      const moved = index % 2 === 0 ? 'running' : 'paused';
      state = mergeProgress(state, later(index, { pass: { ...BASE.pass, state: moved } }), 0);
    }

    expect(state.events).toHaveLength(50);
  });
});
