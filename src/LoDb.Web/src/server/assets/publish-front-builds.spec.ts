import { mkdir, mkdtemp, readFile, readdir, rm, stat, utimes, writeFile } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import { join, sep } from 'node:path';
import { publishFrontBuilds } from './publish-front-builds';

const NOW = new Date('2026-10-10T12:00:00Z');
const LONG_AGO = new Date('2026-08-01T12:00:00Z');
const HASHED = ['main-2ZNNMVSG.js', 'chunk-C1xKWD6m.js', 'styles-ABCD1234.css'];
const HASHED_MEDIA = 'media/beaufort-Q1W2E3R4.woff2';
// Files of public/ and the page shell: they keep their own name, never published.
const UNHASHED = ['index.csr.html', 'sw.js', 'favicon.ico', 'i18n/fr.json', 'offline.html'];

describe('publishFrontBuilds', () => {
  let root: string;
  let browser: string;
  let shared: string;

  async function write(folder: string, file: string, content: string): Promise<void> {
    await mkdir(join(folder, file, '..'), { recursive: true });
    await writeFile(join(folder, file), content);
  }

  async function filesOf(folder: string): Promise<string[]> {
    const entries = await readdir(folder, { recursive: true, withFileTypes: true });
    return entries
      .filter((entry) => entry.isFile())
      .map((entry) => join(entry.parentPath, entry.name).slice(folder.length + 1))
      .map((path) => path.split(sep).join('/'))
      .sort();
  }

  beforeEach(async () => {
    root = await mkdtemp(join(tmpdir(), 'lodb-front-builds-'));
    browser = join(root, 'browser');
    shared = join(root, 'shared');
    await mkdir(shared);
    for (const file of [...HASHED, HASHED_MEDIA, ...UNHASHED]) {
      await write(browser, file, `content of ${file}`);
    }
  });

  afterEach(() => rm(root, { recursive: true, force: true }));

  it('copies the hashed bundles and media, nothing else', async () => {
    expect(await publishFrontBuilds(browser, shared, NOW)).toEqual({ copied: 4, refreshed: 0 });

    expect(await filesOf(shared)).toEqual([...HASHED, HASHED_MEDIA].sort());
    expect(await readFile(join(shared, HASHED_MEDIA), 'utf8')).toBe(`content of ${HASHED_MEDIA}`);
  });

  it('keeps the files of the previous builds', async () => {
    await write(shared, 'chunk-PREVIOUS.js', 'previous build');

    await publishFrontBuilds(browser, shared, NOW);

    expect(await readFile(join(shared, 'chunk-PREVIOUS.js'), 'utf8')).toBe('previous build');
  });

  it('never rewrites a file already there, and dates it from this start', async () => {
    await write(shared, HASHED[0], 'published by an earlier start');
    await utimes(join(shared, HASHED[0]), LONG_AGO, LONG_AGO);

    expect(await publishFrontBuilds(browser, shared, NOW)).toEqual({ copied: 3, refreshed: 1 });

    expect(await readFile(join(shared, HASHED[0]), 'utf8')).toBe('published by an earlier start');
    expect((await stat(join(shared, HASHED[0]))).mtime).toEqual(NOW);
  });

  it('publishes a build without media', async () => {
    await rm(join(browser, 'media'), { recursive: true });

    expect(await publishFrontBuilds(browser, shared, NOW)).toEqual({ copied: 3, refreshed: 0 });
  });
});
