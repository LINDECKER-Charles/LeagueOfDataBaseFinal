import { createLogger } from '../create-logger';
import { SsrErrorHandler } from './ssr-error-handler';

describe('SsrErrorHandler', () => {
  let written: string;
  let consoleError: ReturnType<typeof vi.spyOn>;

  function handle(error: unknown): string[] {
    const logger = createLogger({ write: (chunk: string) => (written += chunk) });
    new SsrErrorHandler(logger).handleError(error);
    return written.split('\n').filter((line) => line.length > 0);
  }

  beforeEach(() => {
    written = '';
    consoleError = vi.spyOn(console, 'error').mockImplementation(() => undefined);
  });

  afterEach(() => {
    consoleError.mockRestore();
  });

  it('writes a render error as one JSON line, not on the console', () => {
    const error = Object.assign(new Error("NG04002: 'u/someone'"), { code: 4002 });

    const lines = handle(error);

    expect(lines).toHaveLength(1);
    expect(JSON.parse(lines[0])).toMatchObject({
      level: 'error',
      msg: 'ssr.render.failed',
      exception: { class: 'Error', code: 4002 },
    });
    expect(written).not.toContain('someone');
    expect(consoleError).not.toHaveBeenCalled();
  });

  it('names the class after the error, not its minified constructor', () => {
    class m extends Error {
      override readonly name = 'RuntimeError';
    }

    expect(JSON.parse(handle(new m('boom'))[0])).toMatchObject({
      exception: { class: 'RuntimeError' },
    });
  });

  it('accepts a thrown value that is not an Error', () => {
    const lines = handle('boom');

    expect(lines).toHaveLength(1);
    expect(JSON.parse(lines[0])).toMatchObject({
      msg: 'ssr.render.failed',
      exception: { class: 'string' },
    });
    expect(consoleError).not.toHaveBeenCalled();
  });
});
