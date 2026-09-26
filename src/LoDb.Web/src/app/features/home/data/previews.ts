import type { ResourceType } from '../../../core/api/generated/models/resource-type';
import type { Preview } from './preview';

/** The preview of every resource; null for one whose list could not be read. */
export type Previews = Readonly<Record<ResourceType, Preview | null>>;
