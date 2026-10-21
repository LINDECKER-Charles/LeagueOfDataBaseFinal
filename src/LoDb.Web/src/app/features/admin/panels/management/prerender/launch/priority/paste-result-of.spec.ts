import { prerenderPatches } from '../../../../../testing/fixtures/prerender-patches';
import { pasteResultOf } from './paste-result-of';

const PATCHES = prerenderPatches();

describe('pasteResultOf', () => {
  it('sorts the older patches, the latest one and the others, in the pasted order', () => {
    expect(pasteResultOf(['16.20.1', '16.18.1', '3.15.9', '16.19.1'], PATCHES)).toEqual({
      known: ['16.18.1', '16.19.1'],
      latest: ['16.20.1'],
      unknown: ['3.15.9'],
      isCapped: false,
    });
  });

  it('says when more than 100 older patches are pasted', () => {
    const versions = Array.from({ length: 101 }, (_, minor) => `14.${minor}.1`);
    const many = prerenderPatches({
      patches: versions.map((version) => ({ ...PATCHES.patches[0], version })),
    });

    expect(pasteResultOf(versions, many).isCapped).toBe(true);
    expect(pasteResultOf(versions.slice(1), many).isCapped).toBe(false);
  });

  it('knows nothing of an empty paste', () => {
    expect(pasteResultOf([], PATCHES)).toEqual({
      known: [],
      latest: [],
      unknown: [],
      isCapped: false,
    });
  });
});
