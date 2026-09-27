import type { ResourceType } from '../../../core/api/generated/models/resource-type';
import type { PageContext } from '../../../core/context/page-context';
import type { HomeSection } from './home-section';

/** What the home route resolves: the context the page reads and a section per resource. */
export interface HomeData {
  /** Null while no version is ingested: the sections then hold their portals alone. */
  readonly context: PageContext | null;
  readonly sections: Readonly<Record<ResourceType, HomeSection>>;
  /**
   * The delay the API asked for before one more read, while a preview still waits for its
   * images (a cold version); null once they are all settled.
   */
  readonly retryAfterMs: number | null;
}
