import { CATALOGUE_METRIC } from './catalogue-metric';
import type { LoadRecord } from './load-record';
import type { LoadTimes } from './load-times';

function initialLoadOf(performance: Performance): LoadTimes {
  const [entry] = performance.getEntriesByType('navigation') as PerformanceNavigationTiming[];
  if (entry === undefined) {
    return { serverMs: null, clientMs: performance.now() };
  }
  const metric = entry.serverTiming?.find((timing) => timing.name === CATALOGUE_METRIC);
  const end = entry.domContentLoadedEventEnd || entry.responseEnd;
  return { serverMs: metric?.duration ?? null, clientMs: end - entry.startTime };
}

/**
 * The times the badge shows for the page just rendered. The first page of a visit reads them
 * from its Navigation Timing entry: the server's `Server-Timing`, and the time to a parsed
 * document. A page reached in the browser counts the entity's fetch as the server's share,
 * and everything from the navigation to its render as the client's.
 */
export function loadTimesOf(record: LoadRecord, performance: Performance): LoadTimes {
  if (record.initial) {
    return initialLoadOf(performance);
  }
  return { serverMs: record.fetchMs, clientMs: performance.now() - record.startedAt };
}
