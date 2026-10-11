import { createLogger } from '../../../create-logger';
import { UnexpectedScripts } from './unexpected-scripts';

describe('UnexpectedScripts', () => {
  let written: string;
  let scripts: UnexpectedScripts;

  const lines = () =>
    written
      .split('\n')
      .filter((line) => line.length > 0)
      .map((line) => JSON.parse(line) as Record<string, unknown>);

  beforeEach(() => {
    written = '';
    scripts = new UnexpectedScripts(createLogger({ write: (chunk: string) => (written += chunk) }));
  });

  it('logs the scripts left out since the previous minute, in one warning', () => {
    scripts.record(2);
    scripts.record(1);

    scripts.flush();
    scripts.flush();

    expect(lines()).toEqual([
      expect.objectContaining({
        level: 'warn',
        msg: 'ssr.csp.unexpected_scripts',
        interval_seconds: 60,
        scripts: 3,
      }),
    ]);
  });

  it('logs nothing for a minute without any', () => {
    scripts.flush();

    expect(lines()).toEqual([]);
  });
});
