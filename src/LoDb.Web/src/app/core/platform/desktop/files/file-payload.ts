import { BridgeError } from '../bridge/bridge-error';

/** The largest file the host saves (`FileContent.MaxBytes` of `LoDb.Desktop`). */
export const MAX_SAVED_BYTES = 20 * 1024 * 1024;
// The host's code for a file over the limit: refused here before it is encoded and sent.
const TOO_LARGE = 'too-large';
// What the host accepts as a media type: `type/subtype`, without parameters.
const MEDIA_TYPE =
  /^[A-Za-z0-9][A-Za-z0-9!#$&^_.+-]{0,126}\/[A-Za-z0-9][A-Za-z0-9!#$&^_.+-]{0,126}$/;
const UNKNOWN_TYPE = 'application/octet-stream';
// `String.fromCharCode` takes its bytes as arguments: a whole file would overflow the stack.
const CHUNK_BYTES = 0x8000;

/** The body of a `saveFile` request. */
export interface SaveFilePayload {
  readonly name: string;
  readonly mime: string;
  readonly base64: string;
}

function base64Of(bytes: Uint8Array): string {
  let binary = '';
  for (let start = 0; start < bytes.length; start += CHUNK_BYTES) {
    binary += String.fromCharCode(...bytes.subarray(start, start + CHUNK_BYTES));
  }
  return btoa(binary);
}

/**
 * A file as the host's `saveFile` takes it: its bytes in base64, its media type without
 * parameters (`text/plain;charset=utf-8` reads `text/plain`), a generic one when the
 * browser named none. The host checks the name; its size is checked here first, to spare
 * encoding and sending twenty megabytes the host would refuse.
 */
export async function saveFilePayload(file: File): Promise<SaveFilePayload> {
  if (file.size > MAX_SAVED_BYTES) {
    throw new BridgeError(TOO_LARGE);
  }
  const type = file.type.split(';')[0]?.trim() ?? '';
  const bytes = new Uint8Array(await file.arrayBuffer());
  return {
    name: file.name,
    mime: MEDIA_TYPE.test(type) ? type : UNKNOWN_TYPE,
    base64: base64Of(bytes),
  };
}
