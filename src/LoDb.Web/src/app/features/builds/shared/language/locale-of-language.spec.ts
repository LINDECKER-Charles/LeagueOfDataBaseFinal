import { localeOfLanguage } from './locale-of-language';

describe('localeOfLanguage', () => {
  it.each([
    ['fr_FR', 'fr'],
    ['en_US', 'en'],
    ['en_GB', 'en'],
    ['pt_BR', 'pt'],
    ['es_MX', 'es'],
    ['ja_JP', 'ja'],
    ['zh_CN', 'zh-hans'],
    ['zh_MY', 'zh-hans'],
    ['zh_TW', 'zh-hant'],
  ])('reads %s in %s', (language, locale) => {
    expect(localeOfLanguage(language)).toBe(locale);
  });

  it.each([
    ['a language no locale speaks', 'nl_NL'],
    ['an empty language', ''],
    ['no language', null],
  ])('falls back on en for %s', (_case, language) => {
    expect(localeOfLanguage(language)).toBe('en');
  });
});
