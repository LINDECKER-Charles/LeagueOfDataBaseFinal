import type { AudienceData } from '../audience-data';
import type { AudienceEventName } from '../audience-event-name';

interface UmamiPayload {
  readonly website: string;
  readonly hostname: string;
  readonly url: string;
  readonly referrer: string;
  readonly title: string;
  readonly language: string;
  readonly screen: string;
  readonly tag: string;
  readonly name?: AudienceEventName;
  readonly data?: AudienceData;
}

/**
 * The body of Umami's `POST /api/send` (Umami 3.4.0, the shape its own tracker posts): a
 * page view without `name`, an event with one. No identifier of the visitor is ever part of
 * it: Umami derives its session from the address and the user agent, rotated daily.
 */
export interface UmamiSendBody {
  readonly type: 'event';
  readonly payload: UmamiPayload;
}
