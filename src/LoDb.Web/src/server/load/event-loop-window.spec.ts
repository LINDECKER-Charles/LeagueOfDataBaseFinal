import { EVENT_LOOP_WINDOW_MS, EventLoopWindow } from './event-loop-window';

describe('EventLoopWindow', () => {
  let window: EventLoopWindow;

  beforeEach(() => {
    vi.useFakeTimers({ toFake: ['setInterval', 'clearInterval'] });
    window = new EventLoopWindow();
    window.start();
  });

  afterEach(() => {
    window.stop();
    vi.useRealTimers();
  });

  it('has no p99 before its first window closes', () => {
    vi.advanceTimersByTime(EVENT_LOOP_WINDOW_MS - 1);

    expect(window.p99Seconds()).toBeNull();
  });

  it('gives the p99 of the last closed window, in seconds, at each rotation', () => {
    vi.advanceTimersByTime(EVENT_LOOP_WINDOW_MS);
    const first = window.p99Seconds();
    vi.advanceTimersByTime(EVENT_LOOP_WINDOW_MS);

    expect(typeof first).toBe('number');
    expect(first).toBeGreaterThanOrEqual(0);
    expect(window.p99Seconds()).toBeGreaterThanOrEqual(0);
  });
});
