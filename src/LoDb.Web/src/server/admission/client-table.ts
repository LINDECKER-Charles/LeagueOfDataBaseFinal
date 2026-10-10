import type { ClientState } from './client-state';

/**
 * The admission state of the clients seen lately, bounded to `capacity` entries: past it,
 * the client seen least recently is forgotten (a Map keeps insertion order, and a client
 * seen again moves to its end). A forgotten client starts afresh, with full budgets.
 */
export class ClientTable {
  private readonly clients = new Map<string, ClientState>();

  constructor(
    private readonly capacity: number,
    private readonly budgetsOf: () => ClientState['budgets'],
  ) {}

  /** The state of the client `key`, made on its first request, now the most recent. */
  stateOf(key: string): ClientState {
    const known = this.clients.get(key);
    if (known !== undefined) {
      this.clients.delete(key);
      this.clients.set(key, known);
      return known;
    }
    const oldest = this.clients.keys().next();
    if (this.clients.size >= this.capacity && oldest.done !== true) {
      this.clients.delete(oldest.value);
    }
    const created: ClientState = { inFlight: 0, budgets: this.budgetsOf() };
    this.clients.set(key, created);
    return created;
  }

  /** Clients remembered now. */
  get size(): number {
    return this.clients.size;
  }
}
