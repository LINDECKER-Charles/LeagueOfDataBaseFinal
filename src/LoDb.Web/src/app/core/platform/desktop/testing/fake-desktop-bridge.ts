import type { BridgeTransport } from '../bridge/bridge-transport';
import type { DesktopMarker } from '../marker/desktop-marker';

/** A request the page sent, as the host would read it. */
export interface SentRequest {
  readonly id: string;
  readonly type: string;
  readonly payload: Readonly<Record<string, unknown>>;
}

/** What the fake host answers a request with; undefined leaves it unanswered. */
export type FakeAnswer = (request: SentRequest) => object | undefined;

/**
 * The host's side of the bridge for the specs: records what the page sends and answers
 * through `answer`, asynchronously as the WebView does, or by hand through `reply`.
 */
export class FakeDesktopBridge {
  readonly sent: SentRequest[] = [];
  answer: FakeAnswer = () => undefined;
  private listener: ((message: string) => void) | null = null;

  readonly transport: BridgeTransport = {
    send: (message) => this.receive(JSON.parse(message) as SentRequest),
    listen: (listener) => {
      this.listener = listener;
    },
  };

  /** The marker of a host of `version` exposing this bridge. */
  marker(version = '1.4.0'): DesktopMarker {
    return { version, bridge: this.transport };
  }

  /** Sends any message to the page, as the host would. */
  reply(message: unknown): void {
    this.listener?.(typeof message === 'string' ? message : JSON.stringify(message));
  }

  /** The types of the requests sent so far, in order. */
  types(): string[] {
    return this.sent.map(({ type }) => type);
  }

  private receive(request: SentRequest): void {
    this.sent.push(request);
    const answer = this.answer(request);
    if (answer !== undefined) {
      queueMicrotask(() => this.reply({ id: request.id, ...answer }));
    }
  }
}
