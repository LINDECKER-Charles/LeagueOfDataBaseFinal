import type { TestRequest } from '@angular/common/http/testing';
import type { AdminVisit } from '../admin-visit';
import type { Answer } from './answer';
import { settle } from './settle';

/** Answers `request` with `body`, then renders the page. */
export async function reply(visit: AdminVisit, request: TestRequest, body: Answer): Promise<void> {
  request.flush(body);
  await settle(visit);
}
