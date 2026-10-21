import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, signal } from '@angular/core';
import { type ComponentFixture, TestBed } from '@angular/core/testing';
import { Subject } from 'rxjs';
import { configureAdminTestBed } from '../../../../../testing/admin-test-bed';
import { injectLivePoll } from './inject-live-poll';

const EVERY_MS = 100;

// A source answered by hand: each read is a subject the spec answers or fails.
@Component({ template: '', changeDetection: ChangeDetectionStrategy.OnPush })
class Host {
  readonly every = signal<number | null>(EVERY_MS);
  readonly hiddenEvery = signal<number | null>(null);
  readonly reads: Subject<number>[] = [];
  readonly lasts: (number | undefined)[] = [];
  readonly poll = injectLivePoll<number>({
    load: (last) => {
      this.lasts.push(last);
      const read = new Subject<number>();
      this.reads.push(read);
      return read;
    },
    every: () => this.every(),
    hiddenEvery: () => this.hiddenEvery(),
  });
}

let visibility: DocumentVisibilityState = 'visible';

function showTab(state: DocumentVisibilityState): void {
  visibility = state;
  document.dispatchEvent(new Event('visibilitychange'));
}

async function wait(fixture: ComponentFixture<Host>, ms: number): Promise<void> {
  fixture.detectChanges();
  await vi.advanceTimersByTimeAsync(ms);
  fixture.detectChanges();
}

async function open(): Promise<ComponentFixture<Host>> {
  const fixture = TestBed.createComponent(Host);
  await wait(fixture, 0);
  return fixture;
}

async function answer(fixture: ComponentFixture<Host>, value: number): Promise<void> {
  fixture.componentInstance.reads.at(-1)?.next(value);
  await wait(fixture, 0);
}

async function fail(fixture: ComponentFixture<Host>, status: number): Promise<void> {
  fixture.componentInstance.reads.at(-1)?.error(new HttpErrorResponse({ status }));
  await wait(fixture, 0);
}

describe('injectLivePoll', () => {
  beforeEach(() => {
    vi.useFakeTimers();
    visibility = 'visible';
    Object.defineProperty(document, 'visibilityState', {
      configurable: true,
      get: () => visibility,
    });
    configureAdminTestBed();
  });

  afterEach(() => {
    delete (document as { visibilityState?: unknown }).visibilityState;
    vi.useRealTimers();
  });

  it('reads at once, then one cadence after each answer, with the last value', async () => {
    const fixture = await open();
    const host = fixture.componentInstance;
    expect(host.reads).toHaveLength(1);

    await answer(fixture, 1);
    expect(host.poll.value()).toBe(1);
    await wait(fixture, EVERY_MS - 1);
    expect(host.reads).toHaveLength(1);
    await wait(fixture, 1);
    expect(host.reads).toHaveLength(2);
    expect(host.lasts).toEqual([undefined, 1]);
  });

  it('never has two reads in flight: a reload waits for the answer', async () => {
    const fixture = await open();
    const host = fixture.componentInstance;

    host.poll.reload();
    await wait(fixture, EVERY_MS * 3);
    expect(host.reads).toHaveLength(1);
    expect(host.poll.busy()).toBe(true);

    await answer(fixture, 1);
    expect(host.reads).toHaveLength(2);
  });

  it('stops once the cadence reads null, and starts again when it does not', async () => {
    const fixture = await open();
    const host = fixture.componentInstance;
    host.every.set(null);
    await answer(fixture, 1);

    await wait(fixture, EVERY_MS * 10);
    expect(host.reads).toHaveLength(1);
    expect(host.poll.nextAttemptAt()).toBeNull();

    host.every.set(EVERY_MS);
    await wait(fixture, 0);
    expect(host.reads).toHaveLength(2);
  });

  it('suspends the reads while the tab is hidden, and reads at once on its return', async () => {
    const fixture = await open();
    const host = fixture.componentInstance;
    await answer(fixture, 1);

    showTab('hidden');
    await wait(fixture, EVERY_MS * 5);
    expect(host.poll.isHidden()).toBe(true);
    expect(host.reads).toHaveLength(1);

    showTab('visible');
    await wait(fixture, 0);
    expect(host.reads).toHaveLength(2);
  });

  it('slows the reads down while hidden when told how', async () => {
    const fixture = await open();
    const host = fixture.componentInstance;
    host.hiddenEvery.set(EVERY_MS * 3);
    await answer(fixture, 1);

    showTab('hidden');
    await wait(fixture, EVERY_MS * 2);
    expect(host.reads).toHaveLength(1);
    await wait(fixture, EVERY_MS);
    expect(host.reads).toHaveLength(2);
  });

  it('keeps the last value through a 502 and backs off before the next read', async () => {
    const fixture = await open();
    const host = fixture.componentInstance;
    await answer(fixture, 1);
    await wait(fixture, EVERY_MS);

    await fail(fixture, 502);
    expect(host.poll.value()).toBe(1);
    expect(host.poll.failure()).toEqual({ kind: 'server', status: 502 });
    expect(host.poll.failures()).toBe(1);
    await wait(fixture, EVERY_MS * 2 - 1);
    expect(host.reads).toHaveLength(2);
    await wait(fixture, 1);
    expect(host.reads).toHaveLength(3);

    await answer(fixture, 2);
    expect(host.poll.failure()).toBeNull();
    expect(host.poll.failures()).toBe(0);
    expect(host.poll.lastSuccessAt()).toBe(Date.now());
  });

  it('tries a read once again after a failure, until a first answer comes', async () => {
    const fixture = await open();
    const host = fixture.componentInstance;
    host.every.set(null);

    await fail(fixture, 502);
    await wait(fixture, 10_000 - 1);
    expect(host.reads).toHaveLength(1);
    await wait(fixture, 1);
    expect(host.reads).toHaveLength(2);

    await answer(fixture, 1);
    await wait(fixture, 600_000);
    expect(host.reads).toHaveLength(2);
    expect(host.poll.nextAttemptAt()).toBeNull();
  });

  it('stops on a 401 until reloaded', async () => {
    const fixture = await open();
    const host = fixture.componentInstance;

    await fail(fixture, 401);
    await wait(fixture, 600_000);
    expect(host.reads).toHaveLength(1);
    expect(host.poll.failure()?.kind).toBe('session');

    host.poll.reload();
    expect(host.reads).toHaveLength(2);
  });
});
