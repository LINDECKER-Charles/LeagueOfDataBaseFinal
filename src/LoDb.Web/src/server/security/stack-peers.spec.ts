import { StackPeers } from './stack-peers';

// A served host's stack subnet: nginx and the renderer.
const STACK_SUBNET = '10.231.1.0/24';

describe('StackPeers', () => {
  const peers = new StackPeers([STACK_SUBNET]);

  it.each(['10.231.1.7', '::ffff:10.231.1.7', '127.0.0.1', '::ffff:127.0.0.1', '::1'])(
    'takes %s for the stack',
    (address) => {
      expect(peers.has(address)).toBe(true);
    },
  );

  // A container of a shared network, the other environment's subnet, a public address, a
  // private IPv6 one, an unparsable value and a socket that has no address any more.
  it.each(['172.20.0.3', '::ffff:172.20.0.3', '10.231.2.7', '203.0.113.9', 'fd00::12', 'x', ''])(
    'refuses %s',
    (address) => {
      expect(peers.has(address)).toBe(false);
    },
  );

  it('refuses a connection without an address', () => {
    expect(peers.has(undefined)).toBe(false);
  });

  it('answers the loopback alone when no network is listed', () => {
    const loopbackOnly = new StackPeers([]);

    expect(loopbackOnly.has('127.0.0.1')).toBe(true);
    expect(loopbackOnly.has('10.231.1.7')).toBe(false);
  });

  it('reads IPv6 networks', () => {
    const v6 = new StackPeers(['fd12:3456::/64']);

    expect(v6.has('fd12:3456::7')).toBe(true);
    expect(v6.has('fd12:3457::7')).toBe(false);
  });

  it.each(['10.231.1.0', '10.231.1.0/33', '10.231.1.0/', '10.231.1.0/24/1', 'lodb/8', '::/129'])(
    'refuses to start with %s',
    (network) => {
      expect(() => new StackPeers([network])).toThrow(/LODB_STACK_NETWORKS must list CIDR/);
    },
  );
});
