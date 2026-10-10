import { TokenBucket } from './token-bucket';

function bucketAt(
  perMinute: number,
  capacity: number,
): { bucket: TokenBucket; wait: (ms: number) => void } {
  let now = 0;
  const bucket = new TokenBucket(perMinute, capacity, () => now);
  return { bucket, wait: (ms) => (now += ms) };
}

function take(bucket: TokenBucket, count: number): boolean[] {
  return Array.from({ length: count }, () => bucket.tryTake());
}

describe('TokenBucket', () => {
  it('starts full, then refuses once the burst is spent', () => {
    const { bucket } = bucketAt(60, 3);

    expect(take(bucket, 4)).toEqual([true, true, true, false]);
  });

  it('earns tokens back at the steady rate', () => {
    const { bucket, wait } = bucketAt(60, 2);
    take(bucket, 2);

    wait(999);
    expect(bucket.tryTake()).toBe(false);
    wait(1);
    expect(bucket.tryTake()).toBe(true);
  });

  it('never holds more than its capacity after a quiet period', () => {
    const { bucket, wait } = bucketAt(60, 2);
    take(bucket, 2);

    wait(60_000);

    expect(take(bucket, 3)).toEqual([true, true, false]);
  });

  it('keeps the fraction earned by a refused take', () => {
    const { bucket, wait } = bucketAt(60, 1);
    bucket.tryTake();

    wait(600);
    expect(bucket.tryTake()).toBe(false);
    wait(400);
    expect(bucket.tryTake()).toBe(true);
  });
});
