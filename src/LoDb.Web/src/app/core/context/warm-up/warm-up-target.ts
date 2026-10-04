import type { ResourceType } from '../../api/generated/models/resource-type';

/** What a loader run warms: a Data Dragon catalog, and the lists its destination shows. */
export interface WarmUpTarget {
  readonly version: string;
  /** Data Dragon language, such as `fr_FR`. */
  readonly language: string;
  /** The lists whose images to fetch; none prepares the datasets alone. */
  readonly resources: readonly ResourceType[];
}
