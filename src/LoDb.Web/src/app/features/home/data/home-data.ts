import type { ResourceType } from '../../../core/api/generated/models/resource-type';
import type { PageContext } from '../../../core/context/page-context';
import type { HomeSection } from './home-section';

/** What the home route resolves: the context the page reads and a section per resource. */
export interface HomeData {
  /** Null while no version is ingested: the sections then hold their portals alone. */
  readonly context: PageContext | null;
  readonly sections: Readonly<Record<ResourceType, HomeSection>>;
}
