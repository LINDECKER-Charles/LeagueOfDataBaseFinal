import { createHash } from 'node:crypto';
import { mkdir, mkdtemp, rm, writeFile } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import type { Request, Response } from 'express';
import { buildFilesEndpoint } from './build-files-endpoint';
import { buildMark } from './build-mark';
import { readBuildListing } from './read-build-listing';

const HASHED = ['styles-QWERTY12.css', 'main-ABCD1234.js', 'chunk-C1xKWD6m.js'];
const HASHED_MEDIA = 'media/font-ZXCV5678.woff2';
// Files of public/ and the templates keep their own name: never part of the list.
const PLAIN = ['index.csr.html', 'favicon.ico', 'i18n/fr.json'];

interface Answer {
  status: number;
  headers: Record<string, string>;
  body?: unknown;
}

function ask(root: string, headers: Record<string, string>): Answer {
  const answer: Answer = { status: 200, headers: {} };
  const request = { get: (name: string) => headers[name.toLowerCase()] };
  const response = {
    status(code: number) {
      answer.status = code;
      return response;
    },
    set(name: string, value: string) {
      answer.headers[name] = value;
      return response;
    },
    type: () => response,
    send(body: unknown) {
      answer.body = body;
      return response;
    },
    json(body: unknown) {
      answer.body = body;
      return response;
    },
  };
  const handler = buildFilesEndpoint(readBuildListing(root));
  handler(request as unknown as Request, response as unknown as Response, () => undefined);
  return answer;
}

async function writeFiles(root: string, files: readonly string[]): Promise<void> {
  for (const file of files) {
    await mkdir(join(root, file, '..'), { recursive: true });
    await writeFile(join(root, file), '');
  }
}

describe('buildFilesEndpoint, over readBuildListing', () => {
  let root: string;

  beforeEach(async () => {
    root = await mkdtemp(join(tmpdir(), 'lodb-build-list-'));
  });

  afterEach(() => rm(root, { recursive: true, force: true }));

  it("lists the build's hashed files, sorted, under the digest of their names", async () => {
    await writeFiles(root, [...HASHED, HASHED_MEDIA, ...PLAIN]);

    const answer = ask(root, { 'x-lodb-prerender': '1' });

    const files = [...HASHED, HASHED_MEDIA].sort();
    const build = createHash('sha256').update(files.join('\n')).digest('hex');
    expect(answer).toEqual({
      status: 200,
      headers: { 'Cache-Control': 'no-store' },
      body: { build, files },
    });
  });

  it('lists the bundles of a build without media', async () => {
    await writeFiles(root, HASHED);

    expect(ask(root, { 'x-lodb-prerender': '1' }).body).toEqual(
      expect.objectContaining({ files: [...HASHED].sort() }),
    );
  });

  it('names the same build as the mark of the pages of the pass', async () => {
    await writeFiles(root, [...HASHED, HASHED_MEDIA]);
    const listing = readBuildListing(root);

    const answer = ask(root, { 'x-lodb-prerender': '1' });

    expect(answer.body).toEqual(expect.objectContaining({ build: listing.build }));
    expect(buildMark('prerender', listing.build)).toEqual({ 'X-LoDb-Build': listing.build });
  });

  it.each<Record<string, string>>([{}, { 'x-lodb-prerender': '' }])(
    'answers a bare 404 without the mark (%o)',
    async (headers) => {
      await writeFiles(root, HASHED);

      expect(ask(root, headers)).toEqual({ status: 404, headers: {}, body: 'Not Found' });
    },
  );
});
