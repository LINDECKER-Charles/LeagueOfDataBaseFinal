import { Injectable, inject } from '@angular/core';
import { Observable, type Subscriber } from 'rxjs';
import { ApiConfiguration } from '../../api/generated/api-configuration';
import { warmUpCatalog } from '../../api/generated/fn/catalog/warm-up-catalog';
import type { WarmUpProgress } from '../../api/generated/models/warm-up-progress';
import type { WarmUpStage } from '../../api/generated/models/warm-up-stage';
import { RequestBuilder } from '../../api/generated/request-builder';
import type { WarmUpTarget } from './warm-up-target';

const FINAL_STAGES: readonly WarmUpStage[] = ['done', 'failed'];
const EVENT_STREAM = 'text/event-stream';

// Each frame is the whole state: a frame that does not parse fails the run, not the page.
function relay(subscriber: Subscriber<WarmUpProgress>, data: string): void {
  let frame: WarmUpProgress;
  try {
    frame = JSON.parse(data) as WarmUpProgress;
  } catch (error) {
    subscriber.error(error);
    return;
  }
  subscriber.next(frame);
  if (FINAL_STAGES.includes(frame.stage)) {
    subscriber.complete();
  }
}

/**
 * The frames of a warm-up, read from its Server-Sent Events stream. The generated client
 * reads no stream: the URL comes from its request builder and its operation path, the
 * frames from an EventSource. The observable completes on the final frame and fails when
 * the connection drops, which an EventSource would otherwise retry forever.
 */
@Injectable({ providedIn: 'root' })
export class WarmUpStream {
  private readonly config = inject(ApiConfiguration);

  open(target: WarmUpTarget): Observable<WarmUpProgress> {
    const url = this.urlOf(target);
    return new Observable<WarmUpProgress>((subscriber) => {
      const source = new EventSource(url);
      source.onmessage = (event: MessageEvent<string>) => relay(subscriber, event.data);
      source.onerror = () => subscriber.error(new Error('The warm-up stream dropped.'));
      return () => source.close();
    });
  }

  private urlOf(target: WarmUpTarget): string {
    const builder = new RequestBuilder(this.config.rootUrl, warmUpCatalog.PATH, 'get');
    builder.path('version', target.version, {});
    builder.path('lang', target.language, {});
    builder.query('resources', [...target.resources], {});
    return builder.build({ responseType: 'text', accept: EVENT_STREAM }).urlWithParams;
  }
}
