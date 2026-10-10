import { clientKeyOf } from './client-key-of';

describe('clientKeyOf', () => {
  it.each([
    ['203.0.113.7', '203.0.113.7'],
    ['::ffff:203.0.113.7', '203.0.113.7'],
    ['::FFFF:203.0.113.7', '203.0.113.7'],
  ])('keys the IPv4 client %s by its address', (address, key) => {
    expect(clientKeyOf(address)).toBe(key);
  });

  it.each([
    '2001:db8:1:2::1',
    '2001:0db8:0001:0002:ffff:ffff:ffff:ffff',
    '2001:db8:1:2:3:4:5:6',
    '2001:db8:1:2::192.0.2.1',
    '2001:db8:1:2::9%eth0',
  ])('keys the IPv6 client %s by its /64', (address) => {
    expect(clientKeyOf(address)).toBe('2001:db8:1:2::/64');
  });

  it.each([
    ['::1', '0:0:0:0::/64'],
    ['2001:db8::', '2001:db8:0:0::/64'],
    ['::2001:db8:1:2:3', '0:0:0:2001::/64'],
  ])('reads the elided groups of %s', (address, key) => {
    expect(clientKeyOf(address)).toBe(key);
  });

  it('keeps two /64 apart', () => {
    expect(clientKeyOf('2001:db8:1:2::1')).not.toBe(clientKeyOf('2001:db8:1:3::1'));
  });

  it.each([undefined, '', 'not an address', '999.1.1.1'])('shares one key for %j', (address) => {
    expect(clientKeyOf(address)).toBe('unknown');
  });
});
