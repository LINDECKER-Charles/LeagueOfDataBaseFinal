import { within } from './within';

describe('within', () => {
  beforeEach(() => {
    vi.useFakeTimers();
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it('settles like the work when it is in time', async () => {
    const result = within(Promise.resolve('work'), 1000, 'fallback');

    expect(await result).toBe('work');
  });

  it('resolves the fallback once the delay has passed', async () => {
    const result = within(new Promise<string>(() => undefined), 1000, 'fallback');
    await vi.advanceTimersByTimeAsync(1000);

    expect(await result).toBe('fallback');
  });

  it('rejects like the work when it fails in time', async () => {
    const result = within(Promise.reject(new Error('down')), 1000, 'fallback');

    await expect(result).rejects.toThrow('down');
  });

  it('leaves no timer behind', async () => {
    await within(Promise.resolve('work'), 1000, 'fallback');

    expect(vi.getTimerCount()).toBe(0);
  });
});
