import { TokenBucket } from './token-bucket';

function bucketAt(
  perMinute: number,
  capacity: number,
): { bucket: TokenBucket; wait: (ms: number) => void } {
  let now = 0;
  const bucket = new TokenBucket(perMinute, capacity, () => now);
  return { bucket, wait: (ms) => (now += ms) };
}

// Spends a token when there is one, as the admission does; tells whether it could.
function tryTake(bucket: TokenBucket): boolean {
  if (!bucket.hasToken()) {
    return false;
  }
  bucket.take();
  return true;
}

function take(bucket: TokenBucket, count: number): boolean[] {
  return Array.from({ length: count }, () => tryTake(bucket));
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
    expect(bucket.hasToken()).toBe(false);
    wait(1);
    expect(bucket.hasToken()).toBe(true);
  });

  it('never holds more than its capacity after a quiet period', () => {
    const { bucket, wait } = bucketAt(60, 2);
    take(bucket, 2);

    wait(60_000);

    expect(take(bucket, 3)).toEqual([true, true, false]);
  });

  it('keeps the fraction earned while it had no token', () => {
    const { bucket, wait } = bucketAt(60, 1);
    take(bucket, 1);

    wait(600);
    expect(bucket.hasToken()).toBe(false);
    wait(400);
    expect(tryTake(bucket)).toBe(true);
  });

  it('spends nothing when only asked', () => {
    const { bucket } = bucketAt(60, 1);

    expect([bucket.hasToken(), bucket.hasToken()]).toEqual([true, true]);
    expect(take(bucket, 2)).toEqual([true, false]);
  });
});
