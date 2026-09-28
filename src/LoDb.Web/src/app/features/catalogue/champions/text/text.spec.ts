import { capitalize } from './capitalize';

describe('capitalize', () => {
  it('raises the first letter only', () => {
    expect(capitalize('the Darkin Blade', 'en')).toBe('The Darkin Blade');
    expect(capitalize('', 'en')).toBe('');
  });

  it('follows the rules of the locale', () => {
    expect(capitalize('ışık', 'tr')).toBe('Işık');
    expect(capitalize('iblis', 'tr')).toBe('İblis');
  });

  it('keeps a text whose first character has no case', () => {
    expect(capitalize('暗裔剑魔', 'zh-hans')).toBe('暗裔剑魔');
  });
});
