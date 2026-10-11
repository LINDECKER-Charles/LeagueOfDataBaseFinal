import type { UrlTree } from '@angular/router';
import type { ResourceType } from '../../../core/api/generated/models/resource-type';

/** A link of a version's row: one of its four lists. */
export interface PatchListLink {
  readonly resource: ResourceType;
  readonly url: UrlTree;
}
