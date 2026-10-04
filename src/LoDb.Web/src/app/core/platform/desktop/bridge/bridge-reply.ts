/** A reply of the host, `{id, ok, result | error}`, once checked. */
export type BridgeReply =
  | { readonly id: string; readonly ok: true; readonly result: Readonly<Record<string, unknown>> }
  | { readonly id: string; readonly ok: false; readonly error: string };

// The code of a failed reply whose `error` is missing or not a string.
const UNREADABLE_ERROR = 'internal';

function parse(message: string): unknown {
  try {
    return JSON.parse(message);
  } catch {
    return null;
  }
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null && !Array.isArray(value);
}

/**
 * Reads a message of the host as untrusted (plan, section 5.2): null unless it is a JSON
 * object with a string `id` and a boolean `ok`. A success without an object `result` reads
 * as an empty result.
 */
export function parseBridgeReply(message: unknown): BridgeReply | null {
  const reply = typeof message === 'string' ? parse(message) : null;
  if (!isRecord(reply) || typeof reply['id'] !== 'string' || typeof reply['ok'] !== 'boolean') {
    return null;
  }
  const id = reply['id'];
  if (reply['ok']) {
    const result = reply['result'];
    return { id, ok: true, result: isRecord(result) ? result : {} };
  }
  const error = reply['error'];
  return { id, ok: false, error: typeof error === 'string' ? error : UNREADABLE_ERROR };
}
