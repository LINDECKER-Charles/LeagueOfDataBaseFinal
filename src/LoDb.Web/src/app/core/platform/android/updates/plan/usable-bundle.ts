import type { LiveUpdateBundle } from '../../../../api/generated/models/live-update-bundle';

// The formats the API enforces at publication (PublishPolicyRules), checked again here: the
// policy is a network answer, and the plugin must never receive a field it would misread.
// `public` is the one id the plugin reserves for the embedded bundle.
const BUNDLE_ID = /^[A-Za-z0-9._-]{1,64}$/;
const RESERVED_ID = 'public';
const CHECKSUM = /^[0-9a-f]{64}$/;
const SIGNATURE = /^[A-Za-z0-9+/]+={0,2}$/;
const RELEASE = /^\d{1,9}\.\d{1,9}\.\d{1,9}$/;
const HTTPS = 'https:';

type Rule = readonly [keyof LiveUpdateBundle, (text: string) => boolean];

const RULES: readonly Rule[] = [
  ['id', (id) => BUNDLE_ID.test(id) && id !== RESERVED_ID],
  ['url', (url) => URL.canParse(url) && new URL(url).protocol === HTTPS],
  ['checksum', (checksum) => CHECKSUM.test(checksum)],
  ['signature', (signature) => SIGNATURE.test(signature)],
  ['minimumNativeVersion', (version) => RELEASE.test(version)],
];

function textOf(value: object, field: keyof LiveUpdateBundle): string {
  const text = (value as Record<string, unknown>)[field];
  return typeof text === 'string' ? text : '';
}

/** The bundle of the policy when every field holds, otherwise null (no usable bundle). */
export function usableBundleOf(value: unknown): LiveUpdateBundle | null {
  if (typeof value !== 'object' || value === null) {
    return null;
  }
  const bundle: LiveUpdateBundle = {
    id: textOf(value, 'id'),
    url: textOf(value, 'url'),
    checksum: textOf(value, 'checksum'),
    signature: textOf(value, 'signature'),
    minimumNativeVersion: textOf(value, 'minimumNativeVersion'),
  };
  return RULES.every(([field, holds]) => holds(bundle[field])) ? bundle : null;
}
