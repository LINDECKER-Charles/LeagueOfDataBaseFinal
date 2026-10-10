import { ClientTable } from './client-table';
import { TokenBucket } from './token-bucket';

const tableOf = (capacity: number) => new ClientTable(capacity, () => new TokenBucket(6, 6));

describe('ClientTable', () => {
  it('gives a client the same state on each request', () => {
    const table = tableOf(2);
    table.stateOf('a').inFlight = 2;

    expect(table.stateOf('a').inFlight).toBe(2);
  });

  it('forgets the client seen least recently past its capacity', () => {
    const table = tableOf(2);
    table.stateOf('a').inFlight = 1;
    table.stateOf('b').inFlight = 1;
    table.stateOf('a');

    table.stateOf('c');

    expect(table.size).toBe(2);
    expect(table.stateOf('a').inFlight).toBe(1);
    expect(table.stateOf('b').inFlight).toBe(0);
  });
});
