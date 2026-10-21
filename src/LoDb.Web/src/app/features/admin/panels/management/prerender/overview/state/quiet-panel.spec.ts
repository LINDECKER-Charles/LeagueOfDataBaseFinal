import { signal } from '@angular/core';
import type { Panel } from '../../../../../state/panel';
import type { PanelFailure } from '../../../../../state/panel-failure';
import { quietPanel } from './quiet-panel';

const BAD_GATEWAY: PanelFailure = { kind: 'server', status: 502 };
const EXPIRED: PanelFailure = { kind: 'session', status: 401 };

function panel(value: number | undefined, failure: PanelFailure | null): Panel<number> {
  return {
    value: signal(value),
    busy: signal(false),
    failure: signal(failure),
    reload: vi.fn(),
  };
}

describe('quietPanel', () => {
  it('says why the first read failed, while nothing is on screen', () => {
    expect(quietPanel(panel(undefined, BAD_GATEWAY)).failure()).toEqual(BAD_GATEWAY);
  });

  it('keeps quiet when a refresh fails over a report on screen', () => {
    expect(quietPanel(panel(1, BAD_GATEWAY)).failure()).toBeNull();
  });

  it('asks to sign in again even over a report on screen', () => {
    expect(quietPanel(panel(1, EXPIRED)).failure()).toEqual(EXPIRED);
  });

  it('reads through to the panel it quiets', () => {
    const source = panel(1, null);
    const quiet = quietPanel(source);
    quiet.reload();

    expect(quiet.value()).toBe(1);
    expect(quiet.failure()).toBeNull();
    expect(source.reload).toHaveBeenCalledOnce();
  });
});
