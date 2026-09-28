import type { Request, Response } from 'express';
import { boundedRequests } from './bounded-requests';

interface Answer {
  status?: number;
  headers: Record<string, string>;
  body?: string;
}

function requestOf(method: string, url: string, headers: Record<string, string> = {}): Request {
  const lowerCased = new Map(
    Object.entries(headers).map(([name, value]) => [name.toLowerCase(), value]),
  );
  return {
    method,
    originalUrl: url,
    get: (name: string) => lowerCased.get(name.toLowerCase()),
  } as unknown as Request;
}

function run(request: Request): { answer: Answer; passed: boolean } {
  const answer: Answer = { headers: {} };
  const response = {
    set(field: string | Record<string, string>, value?: string) {
      Object.assign(answer.headers, typeof field === 'string' ? { [field]: value } : field);
      return response;
    },
    status(code: number) {
      answer.status = code;
      return response;
    },
    type: () => response,
    send(body: string) {
      answer.body = body;
      return response;
    },
  };
  let passed = false;
  boundedRequests()(request, response as unknown as Response, () => {
    passed = true;
  });
  return { answer, passed };
}

describe('boundedRequests', () => {
  it.each(['GET', 'HEAD'])('lets a %s through', (method) => {
    expect(run(requestOf(method, '/en/champions?tags=Mage')).passed).toBe(true);
  });

  it('lets a GET declaring an empty body through', () => {
    expect(run(requestOf('GET', '/en/', { 'Content-Length': '0' })).passed).toBe(true);
  });

  it.each(['POST', 'PUT', 'DELETE', 'PATCH', 'OPTIONS'])('refuses a %s with a 405', (method) => {
    const { answer, passed } = run(requestOf(method, '/en/'));

    expect(passed).toBe(false);
    expect(answer.status).toBe(405);
    expect(answer.headers['Allow']).toBe('GET, HEAD');
    expect(answer.headers['Cache-Control']).toBe('no-store');
  });

  it('refuses an address longer than 4 KiB with a 414', () => {
    const { answer, passed } = run(requestOf('GET', `/en/champions?q=${'a'.repeat(4096)}`));

    expect(passed).toBe(false);
    expect(answer.status).toBe(414);
  });

  it.each<Record<string, string>>([{ 'Content-Length': '12' }, { 'Transfer-Encoding': 'chunked' }])(
    'refuses a GET carrying a body (%o) with a 413',
    (headers) => {
      const { answer, passed } = run(requestOf('GET', '/en/', headers));

      expect(passed).toBe(false);
      expect(answer.status).toBe(413);
      expect(answer.headers['Connection']).toBe('close');
    },
  );
});
