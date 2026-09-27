import { type HttpClient, HttpStatusCode } from '@angular/common/http';
import { Component, signal } from '@angular/core';
import { type ComponentFixture, TestBed } from '@angular/core/testing';
import { HttpTestingController } from '@angular/common/http/testing';
import type { Observable } from 'rxjs';
import type { StrictHttpResponse } from '../../../core/api/generated/strict-http-response';
import { ADMIN_TIMEOUT } from '../shared/http/admin-timeout';
import { ADMIN_API } from '../testing/admin-api';
import { configureAdminTestBed } from '../testing/admin-test-bed';
import { injectPanel } from './inject-panel';
import { PanelState } from './panel-state';

interface Report {
  readonly name: string;
}

interface Query {
  readonly page: number;
}

const REPORT_URL = `${ADMIN_API}/api/admin/report`;

function readReport(http: HttpClient, rootUrl: string, query: Query) {
  return http.get<Report>(`${rootUrl}/api/admin/report`, {
    observe: 'response',
    params: { page: query.page },
  }) as Observable<StrictHttpResponse<Report>>;
}

@Component({
  imports: [PanelState],
  template: `
    <lodb-panel-state [panel]="report" shape="chart" />
    @if (report.value(); as value) {
      <p class="value">{{ value.name }}</p>
    }
  `,
})
class Host {
  readonly page = signal(1);
  readonly report = injectPanel(readReport, () => ({ page: this.page() }));
}

// Short enough for a spec to see a panel give up.
const TIMEOUT_MS = 20;

function open(): { fixture: ComponentFixture<Host>; http: HttpTestingController } {
  configureAdminTestBed([], [{ provide: ADMIN_TIMEOUT, useValue: TIMEOUT_MS }]);
  const fixture = TestBed.createComponent(Host);
  fixture.detectChanges();
  return { fixture, http: TestBed.inject(HttpTestingController) };
}

// The resource adopts an answer a microtask after it arrives.
async function settle(fixture: ComponentFixture<Host>): Promise<void> {
  await fixture.whenStable();
  fixture.detectChanges();
}

function text(fixture: ComponentFixture<Host>): string {
  return (fixture.nativeElement as HTMLElement).textContent ?? '';
}

function element(fixture: ComponentFixture<Host>, selector: string): HTMLElement | null {
  return (fixture.nativeElement as HTMLElement).querySelector<HTMLElement>(selector);
}

describe('injectPanel and PanelState', () => {
  it('reserves the room of the panel until its first answer, then shows it', async () => {
    const { fixture, http } = open();

    expect(element(fixture, '[aria-busy="true"]')).not.toBeNull();
    expect(element(fixture, 'lodb-skeleton.hx-sk-block')).not.toBeNull();

    http.expectOne(`${REPORT_URL}?page=1`).flush({ name: 'first' });
    await settle(fixture);

    expect(text(fixture)).toContain('first');
    expect(element(fixture, '[aria-busy="true"]')).toBeNull();
    expect(fixture.componentInstance.report.busy()).toBe(false);
  });

  it('keeps the last answer on screen while the next query loads', async () => {
    const { fixture, http } = open();
    http.expectOne(`${REPORT_URL}?page=1`).flush({ name: 'first' });
    await settle(fixture);

    fixture.componentInstance.page.set(2);
    fixture.detectChanges();

    expect(fixture.componentInstance.report.busy()).toBe(true);
    expect(text(fixture)).toContain('first');
    http.expectOne(`${REPORT_URL}?page=2`).flush({ name: 'second' });
    await settle(fixture);
    expect(text(fixture)).toContain('second');
  });

  it('gives up after the timeout, then tries again on demand', async () => {
    const { fixture, http } = open();
    http.expectOne(`${REPORT_URL}?page=1`);

    await vi.waitFor(() => expect(fixture.componentInstance.report.failure()).not.toBeNull());
    await settle(fixture);

    expect(fixture.componentInstance.report.failure()).toBe('timeout');
    expect(element(fixture, 'lodb-admin-band[role="alert"]')?.textContent).toContain(
      'admin.state.unavailable',
    );
    element(fixture, '[role="alert"] button')?.click();
    fixture.detectChanges();
    http.expectOne(`${REPORT_URL}?page=1`).flush({ name: 'again' });
    await settle(fixture);
    expect(text(fixture)).toContain('again');
    expect(element(fixture, '[role="alert"]')).toBeNull();
  });

  it.each([
    [HttpStatusCode.Unauthorized, 'session'],
    [HttpStatusCode.Forbidden, 'session'],
    [HttpStatusCode.InternalServerError, 'server'],
  ] as const)('reads an answer %s as a %s failure', async (status, failure) => {
    const { fixture, http } = open();

    http.expectOne(`${REPORT_URL}?page=1`).flush({ code: 'x' }, { status, statusText: 'x' });
    await settle(fixture);

    expect(fixture.componentInstance.report.failure()).toBe(failure);
    expect(element(fixture, '[role="alert"]')?.textContent).toContain('admin.state.unavailable');
  });

  it('offers to sign in again when the session was lost', async () => {
    const { fixture, http } = open();

    http
      .expectOne(`${REPORT_URL}?page=1`)
      .flush(null, { status: HttpStatusCode.Forbidden, statusText: 'Forbidden' });
    await settle(fixture);

    const link = element(fixture, '[role="alert"] a');
    expect(link?.getAttribute('href')).toMatch(/^\/admin\/login\?returnUrl=/);
  });

  it('reads an unreachable API as a network failure', async () => {
    const { fixture, http } = open();

    http.expectOne(`${REPORT_URL}?page=1`).error(new ProgressEvent('error'));
    await settle(fixture);

    expect(fixture.componentInstance.report.failure()).toBe('network');
  });
});
