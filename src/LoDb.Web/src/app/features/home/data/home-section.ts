import type { ResourceType } from '../../../core/api/generated/models/resource-type';
import type { HomeLink } from './home-link';
import type { PreviewCard } from './preview-card';

/** What the home shows of one resource: its portal, its count and its preview. */
export interface HomeSection {
  readonly resource: ResourceType;
  /** The list page of the resource, in the home's context. */
  readonly list: HomeLink;
  /** Entries of the whole list; null when its preview could not be read. */
  readonly total: number | null;
  readonly cards: readonly PreviewCard[];
}
