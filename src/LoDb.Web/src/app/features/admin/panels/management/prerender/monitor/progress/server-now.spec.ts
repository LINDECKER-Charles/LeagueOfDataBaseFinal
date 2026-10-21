import { serverNow } from './server-now';

const GENERATED = '2026-10-21T08:42:05+00:00';

describe('serverNow', () => {
  it('moves the server instant on by the time counted since its answer', () => {
    // The client's clock runs an hour behind the server's: only the gap counts.
    const receivedAt = Date.parse(GENERATED) - 3_600_000;

    expect(serverNow(GENERATED, receivedAt, receivedAt + 4_000)).toBe(
      Date.parse('2026-10-21T08:42:09+00:00'),
    );
  });

  it('is the server instant itself at the reception', () => {
    expect(serverNow(GENERATED, 1_000, 1_000)).toBe(Date.parse(GENERATED));
  });
});
