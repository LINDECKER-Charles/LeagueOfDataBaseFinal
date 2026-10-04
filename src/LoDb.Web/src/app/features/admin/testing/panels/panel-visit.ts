import type { TestRequest } from '@angular/common/http/testing';
import type { AdminVisit } from '../admin-visit';

/** A panel opened by a spec, and the calls it sent when it opened, answered. */
export interface PanelVisit extends AdminVisit {
  readonly calls: readonly TestRequest[];
}
