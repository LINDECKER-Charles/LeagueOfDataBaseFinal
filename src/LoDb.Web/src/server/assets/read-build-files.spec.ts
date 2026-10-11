import { mkdir, mkdtemp, rm, writeFile } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { readBuildFiles } from './read-build-files';

describe('readBuildFiles', () => {
  let root: string;

  async function write(file: string, content: string): Promise<void> {
    await mkdir(join(root, file, '..'), { recursive: true });
    await writeFile(join(root, file), content);
  }

  beforeEach(async () => {
    root = await mkdtemp(join(tmpdir(), 'lodb-build-files-'));
    await write('i18n/fr.json', '{"nav":{"home":"Accueil"}}');
    await write('i18n/seo/zh-hans.json', '{"title":"冠军"}');
    await write('changelog/manifest.json', '{"patches":[]}');
    await write('changelog/notes.txt', 'not json');
    await write('icons/manifest.json', '{"outside":true}');
  });

  afterEach(() => rm(root, { recursive: true, force: true }));

  it('reads the JSON files of the named folders, by their path in the build', () => {
    const files = readBuildFiles(root, ['i18n', 'changelog']);

    expect([...files.keys()].sort()).toEqual([
      'changelog/manifest.json',
      'i18n/fr.json',
      'i18n/seo/zh-hans.json',
    ]);
    expect(files.get('i18n/seo/zh-hans.json')).toEqual({ title: '冠军' });
  });

  it('freezes them: every render reads them, none may change them', () => {
    const catalogue = readBuildFiles(root, ['i18n']).get('i18n/fr.json') as {
      nav: { home: string };
    };

    expect(Object.isFrozen(catalogue)).toBe(true);
    expect(Object.isFrozen(catalogue.nav)).toBe(true);
  });

  it('stops the server on a missing folder or a corrupt file, rather than serve without texts', async () => {
    expect(() => readBuildFiles(root, ['nowhere'])).toThrow();

    await write('i18n/de.json', '{"nav":');
    expect(() => readBuildFiles(root, ['i18n'])).toThrow();
  });
});
