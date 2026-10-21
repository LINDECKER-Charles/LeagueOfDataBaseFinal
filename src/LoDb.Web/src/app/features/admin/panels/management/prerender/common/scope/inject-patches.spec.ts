import { ChangeDetectionStrategy, Component, signal } from '@angular/core';
import { HttpTestingController } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { configureAdminTestBed } from '../../../../../testing/admin-test-bed';
import { prerenderPatches } from '../../../../../testing/fixtures/prerender-patches';
import { sent } from '../../../../../testing/http/sent';
import { injectPatches } from './inject-patches';

const PATCHES = '/api/admin/prerender/patches';

@Component({ template: '', changeDetection: ChangeDetectionStrategy.OnPush })
class Host {
  readonly epoch = signal<number | null>(null);
  readonly patches = injectPatches(() => this.epoch());
}

describe('injectPatches', () => {
  beforeEach(() => configureAdminTestBed());

  it('reads the served epoch, then the epoch chosen, each once', async () => {
    const fixture = TestBed.createComponent(Host);
    const api = { http: TestBed.inject(HttpTestingController) };
    fixture.detectChanges();

    const served = await sent(api, PATCHES);
    expect(served.request.params.has('epoch')).toBe(false);
    served.flush(prerenderPatches());
    await fixture.whenStable();
    expect(fixture.componentInstance.patches.value()?.epoch).toBe(2);

    fixture.componentInstance.epoch.set(3);
    fixture.detectChanges();
    const chosen = await sent(api, PATCHES);
    expect(chosen.request.params.get('epoch')).toBe('3');
    chosen.flush(prerenderPatches({ epoch: 3 }));
    await fixture.whenStable();
    expect(fixture.componentInstance.patches.value()?.epoch).toBe(3);
    api.http.verify();
  });
});
