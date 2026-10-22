import { hasChangesPage } from './has-changes-page';

const VERSIONS = ['16.20.1', '16.19.1', '16.18.1', '16.17.1'];

describe('hasChangesPage', () => {
  it('stops at the end of an index left behind the promotion', () => {
    const meta = {
      versions: VERSIONS,
      oldestChangesVersion: '16.18.1',
      newestChangesVersion: '16.19.1',
    };

    expect(VERSIONS.map((version) => hasChangesPage(version, meta))).toEqual([
      false,
      true,
      true,
      false,
    ]);
  });

  it('keeps every page when the cached list lacks the patch just promoted', () => {
    const meta = {
      versions: VERSIONS,
      oldestChangesVersion: '16.18.1',
      newestChangesVersion: '16.21.1',
    };

    expect(hasChangesPage('16.21.1', meta)).toBe(true);
    expect(VERSIONS.map((version) => hasChangesPage(version, meta))).toEqual([
      true,
      true,
      true,
      false,
    ]);
  });
});
