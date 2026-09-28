import { TestBed } from '@angular/core/testing';
import { BridgeError } from '../bridge/bridge-error';
import { DESKTOP_MARKER } from '../marker/desktop-marker-token';
import { FakeDesktopBridge } from '../testing/fake-desktop-bridge';
import { DesktopUpdates, UPDATE_POLL_MS } from './desktop-updates';

describe('DesktopUpdates', () => {
  let host: FakeDesktopBridge;
  let states: unknown[];

  function updates(withBridge = true): DesktopUpdates {
    const marker = withBridge ? host.marker() : { version: '1.4.0', bridge: null };
    TestBed.configureTestingModule({ providers: [{ provide: DESKTOP_MARKER, useValue: marker }] });
    return TestBed.inject(DesktopUpdates);
  }

  beforeEach(() => {
    vi.useFakeTimers();
    host = new FakeDesktopBridge();
    states = ['none', 'downloading', 'ready'];
    host.answer = ({ type }) =>
      type === 'updateState'
        ? { ok: true, result: { state: states.shift(), version: '1.5.0' } }
        : { ok: false, error: 'no-update' };
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it('follows the host until an update is ready, then stops asking', async () => {
    const service = updates();

    service.start();
    await vi.advanceTimersByTimeAsync(0);
    expect(service.state()).toBe('none');
    await vi.advanceTimersByTimeAsync(UPDATE_POLL_MS);
    expect(service.state()).toBe('downloading');
    await vi.advanceTimersByTimeAsync(UPDATE_POLL_MS);
    expect(service.state()).toBe('ready');
    await vi.advanceTimersByTimeAsync(UPDATE_POLL_MS * 3);

    expect(host.types()).toEqual(['updateState', 'updateState', 'updateState']);
  });

  it('keeps the last state when the host answers something unreadable', async () => {
    states = ['downloading', 'installed'];
    const service = updates();

    service.start();
    await vi.advanceTimersByTimeAsync(UPDATE_POLL_MS);

    expect(service.state()).toBe('downloading');
  });

  it('never asks without a bridge', async () => {
    const service = updates(false);

    service.start();
    await vi.advanceTimersByTimeAsync(UPDATE_POLL_MS);

    expect(service.state()).toBe('none');
    expect(host.sent).toEqual([]);
  });

  it('asks the host to restart, and rejects when it has nothing to apply', async () => {
    const service = updates();
    const applying = service.apply();

    await expect(applying).rejects.toEqual(new BridgeError('no-update'));
    expect(host.types()).toEqual(['applyUpdate']);
  });
});
