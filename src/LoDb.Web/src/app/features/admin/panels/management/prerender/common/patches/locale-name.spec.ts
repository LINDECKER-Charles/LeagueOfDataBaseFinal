import { localeName } from './locale-name';

describe('localeName', () => {
  it('names a locale in French', () => {
    expect(localeName('fr')).toBe('français');
    expect(localeName('ar')).toBe('arabe');
  });

  it('reads the script of a Chinese locale from its code', () => {
    expect(localeName('zh-hans')).toContain('simplifié');
    expect(localeName('zh-hant')).toContain('traditionnel');
  });

  it('keeps a code that names no language', () => {
    expect(localeName('')).toBe('');
  });
});
