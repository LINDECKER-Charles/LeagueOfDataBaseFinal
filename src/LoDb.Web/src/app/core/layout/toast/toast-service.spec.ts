import { PLATFORM_ID } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { ToastService } from './toast-service';

function toasts(platform: 'browser' | 'server' = 'browser'): ToastService {
  TestBed.configureTestingModule({ providers: [{ provide: PLATFORM_ID, useValue: platform }] });
  return TestBed.inject(ToastService);
}

describe('ToastService', () => {
  beforeEach(() => vi.useFakeTimers());
  afterEach(() => vi.useRealTimers());

  it('queues toasts in order, each with its own id', () => {
    const service = toasts();

    service.show('success', 'Saved');
    service.show('error', 'Offline');

    expect(service.toasts()).toEqual([
      { id: 1, kind: 'success', message: 'Saved', leaving: false },
      { id: 2, kind: 'error', message: 'Offline', leaving: false },
    ]);
  });

  it('lets a toast leave on its own after five seconds, once its exit has played', () => {
    const service = toasts();
    service.show('info', 'First');
    vi.advanceTimersByTime(2000);
    service.show('info', 'Second');

    vi.advanceTimersByTime(3000);
    expect(service.toasts().map((toast) => [toast.message, toast.leaving])).toEqual([
      ['First', true],
      ['Second', false],
    ]);

    vi.advanceTimersByTime(220);
    expect(service.toasts().map((toast) => toast.message)).toEqual(['Second']);
    vi.advanceTimersByTime(2000);
    expect(service.toasts()).toEqual([]);
  });

  it('dismisses one toast: its departure gives way to its exit', () => {
    const service = toasts();
    service.show('warning', 'Slow network');
    service.show('info', 'Kept');

    service.dismiss(1);
    service.dismiss(1);

    expect(service.toasts().map((toast) => [toast.id, toast.leaving])).toEqual([
      [1, true],
      [2, false],
    ]);
    expect(vi.getTimerCount()).toBe(2);
    vi.advanceTimersByTime(220);
    expect(service.toasts().map((toast) => toast.id)).toEqual([2]);
  });

  it('ignores an id already gone', () => {
    const service = toasts();
    service.show('info', 'Only');

    service.dismiss(42);

    expect(service.toasts()).toHaveLength(1);
  });

  it('never schedules a departure on the server', () => {
    const service = toasts('server');

    service.show('info', 'Rendered');

    expect(vi.getTimerCount()).toBe(0);
    expect(service.toasts()).toHaveLength(1);
  });

  it('clears pending departures when the application is destroyed', () => {
    const service = toasts();
    service.show('info', 'Pending');

    TestBed.resetTestingModule();

    expect(vi.getTimerCount()).toBe(0);
  });
});
