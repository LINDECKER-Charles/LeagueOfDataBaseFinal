import type { Request, Response } from 'express';
import { networkGate } from './network-gate';
import { StackPeers } from './stack-peers';

const PEERS = new StackPeers(['10.231.1.0/24']);

interface Outcome {
  status?: number;
  isEnded: boolean;
  isNextCalled: boolean;
  refusals: number;
}

function requestFrom(address: string, headers: Record<string, string> = {}): Request {
  return { socket: { remoteAddress: address }, headers } as unknown as Request;
}

function run(request: Request): Outcome {
  const outcome: Outcome = { isEnded: false, isNextCalled: false, refusals: 0 };
  const response = {
    status(code: number) {
      outcome.status = code;
      return response;
    },
    end(...chunks: unknown[]) {
      expect(chunks).toEqual([]);
      outcome.isEnded = true;
      return response;
    },
  };
  networkGate(PEERS, () => (outcome.refusals += 1))(
    request,
    response as unknown as Response,
    () => (outcome.isNextCalled = true),
  );
  return outcome;
}

describe('networkGate', () => {
  it.each(['10.231.1.7', '::ffff:10.231.1.7', '127.0.0.1', '::1'])('lets %s through', (address) => {
    expect(run(requestFrom(address))).toEqual({
      isEnded: false,
      isNextCalled: true,
      refusals: 0,
    });
  });

  it.each(['172.20.0.3', '10.231.2.7', '203.0.113.9'])(
    'answers %s an empty 404 and counts it',
    (address) => {
      expect(run(requestFrom(address))).toEqual({
        status: 404,
        isEnded: true,
        isNextCalled: false,
        refusals: 1,
      });
    },
  );

  it('judges the connection, never a forwarded address', () => {
    const forged = requestFrom('172.20.0.3', { 'x-forwarded-for': '10.231.1.7' });

    expect(run(forged).status).toBe(404);
  });
});
