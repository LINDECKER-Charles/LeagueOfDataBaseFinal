import { versionTokens } from './version-tokens';

describe('versionTokens', () => {
  it('reads one version a line, or a list split by commas', () => {
    expect(versionTokens('16.18.1\n16.12.1\r\n15.2.1')).toEqual(['16.18.1', '16.12.1', '15.2.1']);
    expect(versionTokens('16.18.1, 16.12.1,15.2.1')).toEqual(['16.18.1', '16.12.1', '15.2.1']);
  });

  it('reads the tabulated output of LogsQL, leaving its counts out', () => {
    expect(versionTokens('version\tcount\n16.18.1\t12034\n15.2.1\t870')).toEqual([
      '16.18.1',
      '15.2.1',
    ]);
  });

  it('reads the versions of a JSON document', () => {
    expect(versionTokens('[{"version":"16.18.1","hits":3},{"version":"14.24.1"}]')).toEqual([
      '16.18.1',
      '14.24.1',
    ]);
  });

  it('keeps each version once, at its first rank', () => {
    expect(versionTokens('15.2.1 16.18.1 15.2.1')).toEqual(['15.2.1', '16.18.1']);
  });

  it('leaves out a version glued to a point, at either end', () => {
    expect(versionTokens('1.5.')).toEqual([]);
    expect(versionTokens('.1.5')).toEqual([]);
    expect(versionTokens('see 1.5')).toEqual(['1.5']);
  });
});
