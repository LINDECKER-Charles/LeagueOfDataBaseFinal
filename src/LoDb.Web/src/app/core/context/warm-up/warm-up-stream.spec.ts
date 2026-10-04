import { TestBed } from '@angular/core/testing';
import { API_BASE_URL } from '../../api/api-base-url';
import type { WarmUpProgress } from '../../api/generated/models/warm-up-progress';
import { preparingFrame } from './preparing-frame';
import { WarmUpStream } from './warm-up-stream';
import type { WarmUpTarget } from './warm-up-target';

const ORIGIN = 'http://api.test';
const TARGET: WarmUpTarget = {
  version: '15.14.1',
  language: 'fr_FR',
  resources: ['champions', 'items'],
};
const PREPARING = preparingFrame(TARGET.resources);
const DONE: WarmUpProgress = { ...PREPARING, stage: 'done' };

// jsdom has no EventSource: this one is driven by the test.
class FakeEventSource {
  static last: FakeEventSource;
  onmessage: ((event: MessageEvent<string>) => void) | null = null;
  onerror: (() => void) | null = null;
  closed = false;

  constructor(readonly url: string) {
    FakeEventSource.last = this;
  }

  close(): void {
    this.closed = true;
  }

  send(frame: WarmUpProgress): void {
    this.onmessage?.(new MessageEvent('message', { data: JSON.stringify(frame) }));
  }
}

function stream(): WarmUpStream {
  TestBed.configureTestingModule({ providers: [{ provide: API_BASE_URL, useValue: ORIGIN }] });
  return TestBed.inject(WarmUpStream);
}

describe('WarmUpStream', () => {
  beforeEach(() => vi.stubGlobal('EventSource', FakeEventSource));

  afterEach(() => vi.unstubAllGlobals());

  it('reads the warm-up of the target from the API, its lists in the query', () => {
    stream().open(TARGET).subscribe();

    expect(FakeEventSource.last.url).toBe(
      `${ORIGIN}/api/catalog/15.14.1/fr_FR/warm-up?resources=champions&resources=items`,
    );
  });

  it('relays each frame, then completes on the final one and closes the connection', () => {
    const seen: WarmUpProgress[] = [];
    const complete = vi.fn();
    stream()
      .open(TARGET)
      .subscribe({ next: (frame) => seen.push(frame), complete });

    FakeEventSource.last.send(PREPARING);
    FakeEventSource.last.send(DONE);

    expect(seen).toEqual([PREPARING, DONE]);
    expect(complete).toHaveBeenCalledOnce();
    expect(FakeEventSource.last.closed).toBe(true);
  });

  it('fails when the connection drops, rather than letting it reconnect', () => {
    const error = vi.fn();
    stream().open(TARGET).subscribe({ error });

    FakeEventSource.last.onerror?.();

    expect(error).toHaveBeenCalledOnce();
    expect(FakeEventSource.last.closed).toBe(true);
  });

  it('closes the connection when its reader leaves', () => {
    const reading = stream().open(TARGET).subscribe();

    reading.unsubscribe();

    expect(FakeEventSource.last.closed).toBe(true);
  });
});
