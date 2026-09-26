// W3C Trace Context: version-traceid-parentid-flags in lowercase hex. Versions after 00 may
// append fields; 00 may not, and ff is forbidden.
const TRACEPARENT = /^([0-9a-f]{2})-([0-9a-f]{32})-([0-9a-f]{16})-[0-9a-f]{2}(-.*)?$/;
const FIRST_VERSION = '00';
const FORBIDDEN_VERSION = 'ff';
const ALL_ZEROS = /^0+$/;

/**
 * Trace id carried by a `traceparent` header, or undefined when the header is absent or
 * invalid: a malformed header must not yield an id that would join unrelated requests.
 */
export function traceIdFromTraceparent(header: string | undefined): string | undefined {
  const match = TRACEPARENT.exec(header?.trim() ?? '');
  if (match === null) {
    return undefined;
  }
  const [, version, traceId, parentId, extension] = match;
  const versionValid =
    version !== FORBIDDEN_VERSION && (version !== FIRST_VERSION || extension === undefined);
  const idsValid = !ALL_ZEROS.test(traceId) && !ALL_ZEROS.test(parentId);
  return versionValid && idsValid ? traceId : undefined;
}
