import { languageName } from './language-name';

describe('languageName', () => {
  it('names a Data Dragon language in English, as the legacy site did', () => {
    expect(languageName('fr_FR')).toBe('French');
    expect(languageName('zh_TW')).toBe('Chinese (Traditional)');
  });

  it("names a language the legacy table lacks through Intl's English names", () => {
    expect(languageName('nl_NL')).toBe('Dutch (Netherlands)');
  });

  it('falls back to the code when it is ill-formed', () => {
    expect(languageName('not a language')).toBe('not a language');
  });
});
