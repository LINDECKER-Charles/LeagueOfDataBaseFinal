import { mkdir, mkdtemp, rm, stat, utimes, writeFile } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { createLogger } from '../create-logger';
import { FrontBuildsPublication } from './front-builds-publication';

const BUNDLE = 'main-2ZNNMVSG.js';
const AN_HOUR_MS = 3_600_000;
const LONG_AGO = new Date('2026-08-01T12:00:00Z');

describe('FrontBuildsPublication', () => {
  let root: string;
  let browser: string;
  let written: string;

  const lines = () =>
    written
      .split('\n')
      .filter((line) => line.length > 0)
      .map((line) => JSON.parse(line) as Record<string, unknown>);
  const publication = (shared: string) =>
    new FrontBuildsPublication(
      browser,
      shared,
      createLogger({ write: (chunk: string) => (written += chunk) }),
    );

  beforeEach(async () => {
    vi.useFakeTimers({ toFake: ['setInterval', 'clearInterval'] });
    written = '';
    root = await mkdtemp(join(tmpdir(), 'lodb-front-builds-'));
    browser = join(root, 'browser');
    await mkdir(browser);
    await writeFile(join(browser, BUNDLE), 'bundle');
  });

  afterEach(async () => {
    vi.useRealTimers();
    await rm(root, { recursive: true, force: true });
  });

  it('publishes the build at start, and says so once', async () => {
    const shared = join(root, 'shared');

    publication(shared).start();

    await vi.waitFor(() => expect(lines()).toHaveLength(1));
    expect(lines()[0]).toMatchObject({ level: 'info', msg: 'ssr.builds.published', copied: 1 });
    expect((await stat(join(shared, BUNDLE))).isFile()).toBe(true);
  });

  it('dates the files of the running build again every hour, for the purge', async () => {
    const shared = join(root, 'shared');
    publication(shared).start();
    await vi.waitFor(() => expect(lines()).toHaveLength(1));
    await utimes(join(shared, BUNDLE), LONG_AGO, LONG_AGO);

    vi.advanceTimersByTime(AN_HOUR_MS);

    await vi.waitFor(async () =>
      expect((await stat(join(shared, BUNDLE))).mtime.getTime()).toBeGreaterThan(
        LONG_AGO.getTime(),
      ),
    );
    expect(lines()).toHaveLength(1);
  });

  it('logs a failure, and serves on', async () => {
    const file = join(root, 'not-a-folder');
    await writeFile(file, '');

    publication(join(file, 'shared')).start();

    await vi.waitFor(() => expect(lines()).toHaveLength(1));
    expect(lines()[0]).toMatchObject({ level: 'error', msg: 'ssr.builds.failed' });
  });
});
