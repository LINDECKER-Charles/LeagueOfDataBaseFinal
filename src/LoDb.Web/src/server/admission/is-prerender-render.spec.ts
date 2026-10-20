import type { Request } from 'express';
import { isPrerenderRender } from './is-prerender-render';

function requestWith(value: string | undefined): Request {
  const get = (name: string) => (name === 'x-lodb-prerender' ? value : undefined);
  return { get } as unknown as Request;
}

describe('isPrerenderRender', () => {
  it('recognises the mark of the pre-render pass', () => {
    expect(isPrerenderRender(requestWith('1'))).toBe(true);
  });

  // nginx sends the header empty on public traffic; any other value is no mark either.
  it.each([undefined, '', '0', 'true'])('ignores the header set to %s', (value) => {
    expect(isPrerenderRender(requestWith(value))).toBe(false);
  });
});
