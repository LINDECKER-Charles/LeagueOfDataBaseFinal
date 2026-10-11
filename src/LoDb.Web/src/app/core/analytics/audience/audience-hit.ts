import type { AudienceEventName } from './audience-event-name';
import type { AudiencePage } from './audience-page';
import type { AudienceData } from './audience-data';

/** One thing to send: a page view, or an event named on the page where it happened. */
export interface AudienceHit {
  readonly page: AudiencePage;
  /** The document's title for a public path, the template otherwise. */
  readonly title: string;
  /** Origin of the referring site on the first view of a visit, empty otherwise. */
  readonly referrer: string;
  readonly name?: AudienceEventName;
  /** Data of the event, added to the locale and the version of its page. */
  readonly data?: AudienceData;
}
