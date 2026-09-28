import { QueryString } from './query-string';

describe('QueryString', () => {
  it.each([
    ['', ''],
    ['?', ''],
    ['?page=2', '?page=2'],
    ['page=2&size=20', '?page=2&size=20'],
    ['?tag=Boots%2CArmor&price=0-3000', '?tag=Boots%2CArmor&price=0-3000'],
    ['?q=long+sword&&page=2', '?q=long+sword&page=2'],
    ['?flag&page=1', '?flag&page=1'],
  ])('keeps %j as %j', (search, expected) => {
    expect(QueryString.parse(search).toString()).toBe(expected);
  });

  it.each([
    ['?lang=en_GB', 'lang', 'en_GB'],
    ['?tag=Boots%2CArmor', 'tag', 'Boots,Armor'],
    ['?q=long+sword', 'q', 'long sword'],
    ['?q=%E0%A4%A', 'q', '%E0%A4%A'],
    ['?flag', 'flag', ''],
    ['?lang=fr_FR&lang=en_US', 'lang', 'fr_FR'],
    ['?page=2', 'lang', null],
    ['?la%6Eg=fr_FR', 'lang', 'fr_FR'],
  ])('reads %j: %s = %j', (search, name, expected) => {
    expect(QueryString.parse(search).get(name)).toBe(expected);
  });

  it.each([
    ['?lang=fr_FR&page=2&version=15.14.1', ['lang', 'version'], '?page=2'],
    ['?lang=fr_FR&lang=en_US', ['lang'], ''],
    ['?page=2', ['lang'], '?page=2'],
  ])('removes from %j the parameters %j', (search, names, expected) => {
    expect(
      QueryString.parse(search)
        .without(...names)
        .toString(),
    ).toBe(expected);
  });

  it.each([
    { search: '', name: 'lang', value: 'en_GB', expected: '?lang=en_GB' },
    { search: '?lang=en_GB&page=2', name: 'lang', value: 'fr_FR', expected: '?page=2&lang=fr_FR' },
    {
      search: '?tag=Boots%2CArmor',
      name: 'q',
      value: 'a&b',
      expected: '?tag=Boots%2CArmor&q=a%26b',
    },
  ])('sets in $search $name = $value', ({ search, name, value, expected }) => {
    expect(QueryString.parse(search).with(name, value).toString()).toBe(expected);
  });
});
