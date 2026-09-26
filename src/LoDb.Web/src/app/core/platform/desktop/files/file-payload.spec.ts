import { BridgeError } from '../bridge/bridge-error';
import { MAX_SAVED_BYTES, saveFilePayload } from './file-payload';

describe('saveFilePayload', () => {
  it('encodes the bytes in base64 under the name and type of the file', async () => {
    const file = new File(['{"champion":"Jinx"}'], 'build.json', { type: 'application/json' });

    await expect(saveFilePayload(file)).resolves.toEqual({
      name: 'build.json',
      mime: 'application/json',
      base64: btoa('{"champion":"Jinx"}'),
    });
  });

  it('encodes a file larger than one chunk, bytes over 127 included', async () => {
    const bytes = Uint8Array.from({ length: 70_000 }, (_, index) => index % 256);

    const { base64 } = await saveFilePayload(new File([bytes], 'runes.bin'));

    expect(Uint8Array.from(atob(base64), (c) => c.charCodeAt(0))).toEqual(bytes);
  });

  it.each([
    ['text/plain;charset=utf-8', 'text/plain'],
    ['', 'application/octet-stream'],
    ['not a type', 'application/octet-stream'],
  ])('sends %j as %s', async (type, mime) => {
    await expect(saveFilePayload(new File(['x'], 'x.txt', { type }))).resolves.toMatchObject({
      mime,
    });
  });

  it('refuses a file the host would refuse as too large', async () => {
    const file = new File([new Uint8Array(MAX_SAVED_BYTES + 1)], 'huge.bin');

    await expect(saveFilePayload(file)).rejects.toEqual(new BridgeError('too-large'));
  });
});
