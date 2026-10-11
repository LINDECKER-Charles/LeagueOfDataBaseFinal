import { BlockList, isIP } from 'node:net';

type Family = 'ipv4' | 'ipv6';

const FAMILIES: Readonly<Record<number, Family>> = { 4: 'ipv4', 6: 'ipv6' };
const PREFIX_BITS: Readonly<Record<Family, number>> = { ipv4: 32, ipv6: 128 };
const PREFIX_SEPARATOR = '/';
const ZONE_SEPARATOR = '%';
const LOOPBACK_V4 = '127.0.0.0';
const LOOPBACK_V4_PREFIX = 8;
const LOOPBACK_V6 = '::1';

function familyOf(address: string): Family | undefined {
  return FAMILIES[isIP(address)];
}

// The prefix length of a network of `family`, or undefined when it is not one.
function prefixOf(text: string | undefined, family: Family): number | undefined {
  const bits = text === undefined || text === '' ? Number.NaN : Number(text);
  return Number.isInteger(bits) && bits >= 0 && bits <= PREFIX_BITS[family] ? bits : undefined;
}

/**
 * The peers of the stack itself: the loopback, and the networks of `LODB_STACK_NETWORKS`
 * (nginx's, the stack's own subnet on a served host). An IPv4 address that a dual-stack socket
 * reports as `::ffff:a.b.c.d` is matched as IPv4. A network that is not CIDR stops the server
 * at startup rather than letting it guess which peers to answer.
 */
export class StackPeers {
  private readonly peers = new BlockList();

  constructor(networks: readonly string[]) {
    this.peers.addSubnet(LOOPBACK_V4, LOOPBACK_V4_PREFIX, 'ipv4');
    this.peers.addAddress(LOOPBACK_V6, 'ipv6');
    networks.forEach((network) => this.addNetwork(network));
  }

  /** Whether a connection from `address` (a socket's remote address) comes from the stack. */
  has(address: string | undefined): boolean {
    const unzoned = (address ?? '').split(ZONE_SEPARATOR, 1)[0];
    const family = familyOf(unzoned);
    return family !== undefined && this.peers.check(unzoned, family);
  }

  private addNetwork(network: string): void {
    const [address, prefix, ...rest] = network.split(PREFIX_SEPARATOR);
    const family = familyOf(address);
    const bits = family === undefined ? undefined : prefixOf(prefix, family);
    if (family === undefined || bits === undefined || rest.length > 0) {
      throw new Error(`LODB_STACK_NETWORKS must list CIDR networks, got "${network}".`);
    }
    this.peers.addSubnet(address, bits, family);
  }
}
