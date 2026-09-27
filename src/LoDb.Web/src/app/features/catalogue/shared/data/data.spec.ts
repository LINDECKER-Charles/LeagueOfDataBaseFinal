import { type Observable, of } from 'rxjs';
import { retryAfterOf } from '../../../../core/http/retry-after-of';
import { PAGE_SIZE_ALL } from '../filtering/grid/page-size-all';
import type { ListOutcome } from './list-outcome';
import { pageRequestOf } from './page-request-of';
import { withOneRetry } from './with-one-retry';

describe('retryAfterOf', () => {
  const now = Date.parse('2026-09-26T10:00:00Z');

  it('reads whole seconds and HTTP dates', () => {
    expect(retryAfterOf('5', now)).toBe(5000);
    expect(retryAfterOf(' 0 ', now)).toBe(0);
    expect(retryAfterOf('Sat, 26 Sep 2026 10:00:07 GMT', now)).toBe(7000);
  });

  it('asks for no delay after a date already past, and nothing without a readable value', () => {
    expect(retryAfterOf('Sat, 26 Sep 2026 09:00:00 GMT', now)).toBe(0);
    expect(retryAfterOf(null, now)).toBeNull();
    expect(retryAfterOf('soon', now)).toBeNull();
  });
});

describe('pageRequestOf', () => {
  it('asks for one page, or for the whole list beyond what the API pages', () => {
    expect(pageRequestOf(2, 24)).toEqual({ page: 2, size: 24 });
    expect(pageRequestOf(1, PAGE_SIZE_ALL)).toEqual({});
    expect(pageRequestOf(1, 500)).toEqual({});
  });
});

describe('withOneRetry', () => {
  beforeEach(() => vi.useFakeTimers());
  afterEach(() => vi.useRealTimers());

  function run(answers: ListOutcome<string>[]) {
    const calls: number[] = [];
    const seen: ListOutcome<string>[] = [];
    const fetch = (): Observable<ListOutcome<string>> => {
      calls.push(Date.now());
      return of(answers[Math.min(calls.length - 1, answers.length - 1)]);
    };
    withOneRetry(fetch).subscribe((outcome) => seen.push(outcome));
    return { calls, seen };
  }

  it('answers once when nothing asks for a retry', () => {
    const { calls, seen } = run([{ kind: 'list', list: 'ready', retryAfterMs: null }]);
    vi.advanceTimersByTime(60_000);
    expect(calls).toHaveLength(1);
    expect(seen).toEqual([{ kind: 'list', list: 'ready', retryAfterMs: null }]);
  });

  it('retries once after the delay, then stops even if asked again', () => {
    const placeholders: ListOutcome<string> = { kind: 'list', list: 'cold', retryAfterMs: 5000 };
    const { calls, seen } = run([placeholders, placeholders]);
    vi.advanceTimersByTime(4999);
    expect(calls).toHaveLength(1);
    vi.advanceTimersByTime(1);
    expect(calls).toHaveLength(2);
    vi.advanceTimersByTime(60_000);
    expect(calls).toHaveLength(2);
    expect(seen).toHaveLength(2);
  });

  it('retries a pending version once, and never a failure', () => {
    const pending = run([
      { kind: 'pending', retryAfterMs: 5000 },
      { kind: 'list', list: 'warm', retryAfterMs: null },
    ]);
    vi.advanceTimersByTime(5000);
    expect(pending.seen.map((outcome) => outcome.kind)).toEqual(['pending', 'list']);
    const failed = run([{ kind: 'failed' }]);
    vi.advanceTimersByTime(60_000);
    expect(failed.calls).toHaveLength(1);
  });
});
