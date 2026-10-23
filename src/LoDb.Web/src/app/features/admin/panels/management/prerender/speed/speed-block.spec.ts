import { HttpTestingController } from '@angular/common/http/testing';
import { type ComponentFixture, TestBed } from '@angular/core/testing';
import type { PrerenderSpeedReport } from '../../../../../../core/api/generated/models/prerender-speed-report';
import { configureAdminTestBed } from '../../../../testing/admin-test-bed';
import { prerenderReport } from '../../../../testing/fixtures/prerender-report';
import { prerenderSpeed } from '../../../../testing/fixtures/prerender-speed';
import { sent } from '../../../../testing/http/sent';
import { SpeedBlock } from './speed-block';

const SPEED = '/api/admin/prerender/speed';

type Fixture = ComponentFixture<SpeedBlock>;

async function open(speed: PrerenderSpeedReport = prerenderSpeed()) {
  configureAdminTestBed();
  const api = { http: TestBed.inject(HttpTestingController) };
  const fixture = TestBed.createComponent(SpeedBlock);
  fixture.componentRef.setInput('report', prerenderReport());
  fixture.detectChanges();
  (await sent(api, SPEED)).flush(speed);
  await fixture.whenStable();
  fixture.detectChanges();
  return { api, fixture, page: fixture.nativeElement as HTMLElement };
}

function element<T extends HTMLElement>(page: HTMLElement, selector: string): T {
  const found = page.querySelector<T>(selector);
  if (!found) {
    throw new Error(`No ${selector}.`);
  }
  return found;
}

function pick(fixture: Fixture, mode: string): void {
  const page = fixture.nativeElement as HTMLElement;
  element<HTMLInputElement>(page, `[data-mode="${mode}"] input`).click();
  fixture.detectChanges();
}

function type(fixture: Fixture, value: string): void {
  const input = element<HTMLInputElement>(
    fixture.nativeElement as HTMLElement,
    '[data-speed-rate]',
  );
  input.value = value;
  input.dispatchEvent(new Event('change'));
  fixture.detectChanges();
}

describe('SpeedBlock', () => {
  it('shows the stored mode, the resources and the forecast at the ceiling', async () => {
    const { page } = await open();

    expect(element<HTMLInputElement>(page, '[data-mode="adaptive"] input').checked).toBe(true);
    expect(page.querySelector('[data-speed-rate]')).toBeNull();
    expect(page.querySelectorAll('[data-speed-now] dt')).toHaveLength(4);
    expect(page.querySelectorAll('[data-speed-forecast] dt')).toHaveLength(6);
    expect(element<HTMLButtonElement>(page, '[data-speed-apply]').disabled).toBe(true);
  });

  it('sets a manual pace within the hard maximum, then reads the speed again', async () => {
    const { api, fixture, page } = await open();

    pick(fixture, 'manual');
    type(fixture, '9000');
    expect(page.querySelector('[data-speed-brake]')).not.toBeNull();
    element<HTMLButtonElement>(page, '[data-speed-apply]').click();

    const request = await sent(api, SPEED, 'PUT');
    expect(request.request.body).toEqual({ mode: 'manual', rendersPerMinute: 4000 });
    request.flush(null, { status: 204, statusText: 'No Content' });
    await fixture.whenStable();
    (await sent(api, SPEED)).flush(
      prerenderSpeed({
        setting: { mode: 'manual', rendersPerMinute: 4000, updatedAt: '2026-10-21T21:40:00Z' },
      }),
    );
    await fixture.whenStable();
    fixture.detectChanges();
    expect(element(page, '[data-speed-outcome]').textContent).toContain(
      'admin.prerender.done.speed',
    );
    expect(element<HTMLButtonElement>(page, '[data-speed-apply]').disabled).toBe(true);
  });

  it('warns above the threshold of the renderers, not at it nor below', async () => {
    const { fixture, page } = await open();

    pick(fixture, 'manual');
    type(fixture, '1300');
    expect(page.querySelector('[data-speed-warn]')).toBeNull();
    type(fixture, '1301');
    expect(page.querySelector('[data-speed-warn]')).not.toBeNull();
    type(fixture, '600');
    expect(page.querySelector('[data-speed-warn]')).toBeNull();
  });

  it('locks the setting under the kill switch', async () => {
    const { page } = await open(prerenderSpeed({ isFixed: true }));

    expect(page.querySelector('[data-speed-fixed]')).not.toBeNull();
    expect(element<HTMLFieldSetElement>(page, 'fieldset').disabled).toBe(true);
  });
});
