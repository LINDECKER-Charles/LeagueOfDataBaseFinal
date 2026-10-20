import { EventEmitter } from 'node:events';
import type { Request, Response } from 'express';
import { SHED_MARKER } from './admission/shed-marker';
import { createLogger } from './create-logger';
import { requestLogging } from './request-logging';

describe('requestLogging', () => {
  let written: string;

  function serve(
    status: number,
    locals: Record<string, unknown> = {},
    headers: Record<string, string> = {},
  ): Record<string, unknown>[] {
    const logger = createLogger({ write: (chunk: string) => (written += chunk) });
    const get = (name: string): string | undefined => headers[name.toLowerCase()];
    const request = { method: 'GET', get } as unknown as Request;
    const response = Object.assign(new EventEmitter(), { statusCode: status, locals });
    requestLogging(logger)(request, response as unknown as Response, () => undefined);
    response.emit('finish');
    return written
      .split('\n')
      .filter((line) => line.length > 0)
      .map((line) => JSON.parse(line) as Record<string, unknown>);
  }

  beforeEach(() => (written = ''));

  it.each([
    [200, 'info'],
    [404, 'info'],
    [500, 'error'],
    [502, 'error'],
    [503, 'warn'],
  ])('logs a %i at %s', (status, level) => {
    expect(serve(status)).toEqual([
      expect.objectContaining({ level, status, msg: 'http.request.served' }),
    ]);
  });

  it('leaves a render the admission refused to the minute summary', () => {
    expect(serve(503, { [SHED_MARKER]: true })).toEqual([]);
  });

  it.each([200, 301, 404])('writes no line for a %i of the pre-render pass', (status) => {
    expect(serve(status, {}, { 'x-lodb-prerender': '1' })).toEqual([]);
  });

  it.each([
    [502, 'error'],
    [503, 'warn'],
  ])('still logs a %i of the pre-render pass at %s', (status, level) => {
    expect(serve(status, {}, { 'x-lodb-prerender': '1' })).toEqual([
      expect.objectContaining({ level, status, msg: 'http.request.served' }),
    ]);
  });

  it('leaves a render of the pass the admission refused to the minute summary', () => {
    expect(serve(503, { [SHED_MARKER]: true }, { 'x-lodb-prerender': '1' })).toEqual([]);
  });

  it('logs a request whose pre-render header nginx emptied', () => {
    expect(serve(200, {}, { 'x-lodb-prerender': '' })).toHaveLength(1);
  });
});
