import { Injectable, inject } from '@angular/core';
import { DESKTOP_MARKER } from '../marker/desktop-marker-token';
import { BridgeError } from './bridge-error';
import { parseBridgeReply } from './bridge-reply';
import type { BridgeTransport } from './bridge-transport';

/** Long enough for a busy host, short enough that a lost reply does not hang the page. */
export const BRIDGE_TIMEOUT_MS = 10_000;
// No bridge in the page: the smoke check, or the system browser of the fallback.
const UNAVAILABLE = 'unavailable';
const TIMEOUT = 'timeout';
const ID_PREFIX = 'lodb-';

interface PendingRequest {
  readonly resolve: (result: Readonly<Record<string, unknown>>) => void;
  readonly reject: (error: BridgeError) => void;
  readonly timer: ReturnType<typeof setTimeout>;
}

/**
 * The page's side of the host bridge (plan, section 5.2): `{id, type, payload}` out,
 * `{id, ok, result | error}` back. The host may answer out of order, so each request waits
 * for the reply bearing its id, until a deadline. Every message of the host is read as
 * untrusted: anything unreadable, or answering no pending request, is dropped.
 */
@Injectable({ providedIn: 'root' })
export class BridgeClient {
  private readonly transport: BridgeTransport | null = inject(DESKTOP_MARKER)?.bridge ?? null;
  private readonly pending = new Map<string, PendingRequest>();
  private sequence = 0;

  constructor() {
    this.transport?.listen((message) => this.settle(message));
  }

  /** Whether the page has a bridge; without one, every request fails `unavailable`. */
  get isAvailable(): boolean {
    return this.transport !== null;
  }

  /** Sends a command and resolves with its `result`; a refusal rejects with a BridgeError. */
  request(
    type: string,
    payload: object = {},
    timeoutMs = BRIDGE_TIMEOUT_MS,
  ): Promise<Readonly<Record<string, unknown>>> {
    const transport = this.transport;
    if (transport === null) {
      return Promise.reject(new BridgeError(UNAVAILABLE));
    }
    const id = `${ID_PREFIX}${++this.sequence}`;
    return new Promise((resolve, reject) => {
      const timer = setTimeout(() => this.fail(id, TIMEOUT), timeoutMs);
      this.pending.set(id, { resolve, reject, timer });
      try {
        transport.send(JSON.stringify({ id, type, payload }));
      } catch {
        this.fail(id, UNAVAILABLE);
      }
    });
  }

  private settle(message: unknown): void {
    const reply = parseBridgeReply(message);
    const request = reply === null ? undefined : this.take(reply.id);
    if (reply === null || request === undefined) {
      return;
    }
    if (reply.ok) {
      request.resolve(reply.result);
    } else {
      request.reject(new BridgeError(reply.error));
    }
  }

  private fail(id: string, code: string): void {
    this.take(id)?.reject(new BridgeError(code));
  }

  // A request settles once: a late reply after its deadline finds nothing to settle.
  private take(id: string): PendingRequest | undefined {
    const request = this.pending.get(id);
    if (request !== undefined) {
      clearTimeout(request.timer);
      this.pending.delete(id);
    }
    return request;
  }
}
