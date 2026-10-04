import { HttpTestingController } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { RouterTestingHarness } from '@angular/router/testing';
import type { AdminVisit } from './admin-visit';

/**
 * Opens `url` among the routes of the test bed. The page is stable only once its calls are
 * answered: `answer` answers them while the navigation waits.
 */
export async function openAdminPage(
  url: string,
  answer: (http: HttpTestingController) => Promise<void> = () => Promise.resolve(),
): Promise<AdminVisit> {
  const http = TestBed.inject(HttpTestingController);
  const created = RouterTestingHarness.create(url);
  await answer(http);
  const harness = await created;
  // A resource adopts an answer a microtask after it arrives: render once it has.
  await harness.fixture.whenStable();
  harness.detectChanges();
  return { harness, page: harness.routeNativeElement as HTMLElement, http };
}
