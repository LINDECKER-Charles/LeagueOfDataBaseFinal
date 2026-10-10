import { isIPv4, isIPv6 } from 'node:net';

// One client is one IPv4 address, or one IPv6 /64: a host gets a whole /64 to itself, and
// counting its addresses apart would hand it billions of budgets.
const IPV6_GROUPS = 8;
const PREFIX_GROUPS = 4;
const HEXADECIMAL = 16;
const IPV4_MAPPED = /^::ffff:(\d{1,3}(?:\.\d{1,3}){3})$/i;
const ZONE_SEPARATOR = '%';
const GROUP_SEPARATOR = ':';
const ELISION = '::';
const UNKNOWN_CLIENT = 'unknown';

// The groups of one side of `::`; a dotted IPv4 tail stands for the last two.
function groupsOf(part: string | undefined): string[] {
  if (part === undefined || part === '') {
    return [];
  }
  return part
    .split(GROUP_SEPARATOR)
    .flatMap((group) => (group.includes('.') ? ['0', '0'] : [group]));
}

// The four first groups, without leading zeros: one key whatever the spelling of the address.
function prefixOf(address: string): string {
  const [head, tail] = address.split(ELISION);
  const headGroups = groupsOf(head);
  const tailGroups = groupsOf(tail);
  const elided = Array<string>(IPV6_GROUPS - headGroups.length - tailGroups.length).fill('0');
  return [...headGroups, ...elided, ...tailGroups]
    .slice(0, PREFIX_GROUPS)
    .map((group) => parseInt(group, HEXADECIMAL).toString(HEXADECIMAL))
    .join(GROUP_SEPARATOR);
}

/**
 * The client a request counts against in the admission, from the address Express reads
 * behind nginx (`trust proxy`): the IPv4 address itself, an IPv4-mapped IPv6 one included,
 * or the /64 of an IPv6 address. Anything else (no address left) shares one key.
 */
export function clientKeyOf(address: string | undefined): string {
  const unzoned = (address ?? '').split(ZONE_SEPARATOR, 1)[0];
  const candidate = IPV4_MAPPED.exec(unzoned)?.[1] ?? unzoned;
  if (isIPv4(candidate)) {
    return candidate;
  }
  return isIPv6(candidate) ? `${prefixOf(candidate)}::/64` : UNKNOWN_CLIENT;
}
