import { traceIdFromTraceparent } from './trace-id-from-traceparent';

describe('traceIdFromTraceparent', () => {
  const traceId = '4bf92f3577b34da6a3ce929d0e0e4736';
  const parentId = '00f067aa0ba902b7';

  it('reads the trace id of a valid header', () => {
    expect(traceIdFromTraceparent(`00-${traceId}-${parentId}-01`)).toBe(traceId);
    expect(traceIdFromTraceparent(` 00-${traceId}-${parentId}-00 `)).toBe(traceId);
  });

  it('accepts later versions and their extra fields', () => {
    expect(traceIdFromTraceparent(`01-${traceId}-${parentId}-01-future`)).toBe(traceId);
  });

  it.each([
    undefined,
    '',
    'garbage',
    `00-${traceId}-${parentId}`,
    `00-${traceId.toUpperCase()}-${parentId}-01`,
    `00-${traceId}-${parentId}-01-extra`,
    `ff-${traceId}-${parentId}-01`,
    `00-${'0'.repeat(32)}-${parentId}-01`,
    `00-${traceId}-${'0'.repeat(16)}-01`,
    `00-${traceId}0-${parentId}-01`,
  ])('ignores %j', (header) => {
    expect(traceIdFromTraceparent(header)).toBeUndefined();
  });
});
