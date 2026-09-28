import { TestBed } from '@angular/core/testing';
import { DESKTOP_MARKER } from '../marker/desktop-marker-token';
import { FakeDesktopBridge } from '../testing/fake-desktop-bridge';
import { BRIDGE_TIMEOUT_MS, BridgeClient } from './bridge-client';
import { BridgeError } from './bridge-error';

describe('BridgeClient', () => {
  let host: FakeDesktopBridge;

  function client(withBridge = true): BridgeClient {
    const marker = withBridge ? host.marker() : { version: '1.4.0', bridge: null };
    TestBed.configureTestingModule({ providers: [{ provide: DESKTOP_MARKER, useValue: marker }] });
    return TestBed.inject(BridgeClient);
  }

  beforeEach(() => {
    host = new FakeDesktopBridge();
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it('sends each request with its own id, type and payload', () => {
    const bridge = client();

    void bridge.request('openExternal', { url: 'https://x.example/' });
    void bridge.request('updateState');

    const [first, second] = host.sent;
    expect([first?.type, second?.type]).toEqual(['openExternal', 'updateState']);
    expect(first?.payload).toEqual({ url: 'https://x.example/' });
    expect(second?.payload).toEqual({});
    expect(first?.id).not.toBe(second?.id);
    expect(first?.id.length).toBeLessThanOrEqual(64);
  });

  it('settles each request with the reply bearing its id, in any order', async () => {
    const bridge = client();
    const first = bridge.request('updateState');
    const second = bridge.request('openExternal', { url: 'https://x.example/' });

    host.reply({ id: host.sent[1]?.id, ok: false, error: 'invalid-url' });
    host.reply({ id: host.sent[0]?.id, ok: true, result: { state: 'ready', version: '1.5.0' } });

    await expect(first).resolves.toEqual({ state: 'ready', version: '1.5.0' });
    await expect(second).rejects.toEqual(new BridgeError('invalid-url'));
  });

  it.each([
    'not json',
    '[]',
    '{"ok":true,"result":{}}',
    '{"id":7,"ok":true}',
    '{"id":"lodb-1","ok":"yes"}',
    '{"id":"someone-else","ok":true,"result":{}}',
  ])('drops a message it cannot trust (%s)', async (message) => {
    vi.useFakeTimers();
    const request = client().request('updateState');

    host.reply(message);
    vi.advanceTimersByTime(BRIDGE_TIMEOUT_MS);

    await expect(request).rejects.toEqual(new BridgeError('timeout'));
  });

  it('rejects a request the host never answers once its deadline passed', async () => {
    vi.useFakeTimers();
    const bridge = client();
    const request = bridge.request('saveFile', {}, 500);
    const rejected = expect(request).rejects.toEqual(new BridgeError('timeout'));

    vi.advanceTimersByTime(499);
    await Promise.resolve();
    vi.advanceTimersByTime(1);

    await rejected;
    // The late reply finds nothing to settle.
    host.reply({ id: host.sent[0]?.id, ok: true, result: {} });
  });

  it('reads a result-less success as empty, a code-less failure as internal', async () => {
    const bridge = client();
    const done = bridge.request('openExternal');
    const failed = bridge.request('applyUpdate');

    host.reply({ id: host.sent[0]?.id, ok: true, result: 'yes' });
    host.reply({ id: host.sent[1]?.id, ok: false });

    await expect(done).resolves.toEqual({});
    await expect(failed).rejects.toEqual(new BridgeError('internal'));
  });

  it('fails every request without a bridge', async () => {
    const bridge = client(false);

    expect(bridge.isAvailable).toBe(false);
    await expect(bridge.request('updateState')).rejects.toEqual(new BridgeError('unavailable'));
  });

  it('fails a request the transport could not send', async () => {
    host.transport.send = () => {
      throw new Error('window.external is gone');
    };

    await expect(client().request('updateState')).rejects.toEqual(new BridgeError('unavailable'));
  });
});
