import type { HttpTestingController } from '@angular/common/http/testing';
import type { RouterTestingHarness } from '@angular/router/testing';

/** A page of the admin opened by a spec, and the API it calls. */
export interface AdminVisit {
  readonly harness: RouterTestingHarness;
  readonly page: HTMLElement;
  readonly http: HttpTestingController;
}
