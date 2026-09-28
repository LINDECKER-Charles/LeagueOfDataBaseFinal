import type { Signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Subject } from 'rxjs';
import { DialogService } from '../../../ui/overlays/dialog-service';
import type { WarmUpProgress } from '../../api/generated/models/warm-up-progress';
import { LoaderDialog } from './dialog/loader-dialog';
import { preparingFrame } from './preparing-frame';
import { WarmUpLoader } from './warm-up-loader';
import { WarmUpStream } from './warm-up-stream';
import type { WarmUpTarget } from './warm-up-target';

const TARGET: WarmUpTarget = { version: '15.14.1', language: 'fr_FR', resources: ['items'] };
const IMAGES: WarmUpProgress = { ...preparingFrame(['items']), stage: 'images', total: 4 };
const DONE: WarmUpProgress = { ...IMAGES, stage: 'done', settled: 4 };
const READY_HOLD_MS = 450;
const WATCHDOG_IDLE_MS = 15_000;

interface Deferred {
  readonly promise: Promise<boolean>;
  readonly resolve: (landed: boolean) => void;
}

function deferred(): Deferred {
  let resolve: (landed: boolean) => void = () => undefined;
  const promise = new Promise<boolean>((settle) => (resolve = settle));
  return { promise, resolve };
}

// The modal loads with the first switch: the run starts once it has opened.
async function setup() {
  const frames = new Subject<WarmUpProgress>();
  const closed = new Subject<void>();
  const dialog = { closed, close: vi.fn(() => closed.next()) };
  const dialogs = { open: vi.fn(() => dialog) };
  TestBed.configureTestingModule({
    providers: [
      { provide: DialogService, useValue: dialogs },
      { provide: WarmUpStream, useValue: { open: vi.fn(() => frames) } },
    ],
  });
  const landing = deferred();
  const visit = vi.fn(() => landing.promise);
  const gated = TestBed.inject(WarmUpLoader).gate(TARGET, visit);
  await vi.waitFor(() => expect(dialogs.open).toHaveBeenCalled());
  const shown = (): WarmUpProgress =>
    (
      dialogs.open.mock.calls[0] as unknown as [unknown, { data: Signal<WarmUpProgress> }]
    )[1].data();
  return { frames, closed, dialog, dialogs, landing, visit, gated, shown };
}

describe('WarmUpLoader', () => {
  beforeEach(() => vi.useFakeTimers());

  afterEach(() => vi.useRealTimers());

  it('opens the modal at once, on the lists of the target, before any frame', async () => {
    const { dialogs, visit, shown } = await setup();

    expect(dialogs.open).toHaveBeenCalledExactlyOnceWith(
      LoaderDialog,
      expect.objectContaining({ labelledBy: LoaderDialog.HEADING_ID, size: 'compact' }),
    );
    expect(shown()).toEqual(preparingFrame(['items']));
    expect(visit).not.toHaveBeenCalled();
  });

  it('shows each frame, then visits after a readable beat once done', async () => {
    const { frames, visit, shown } = await setup();

    frames.next(IMAGES);
    expect(shown()).toEqual(IMAGES);
    frames.next(DONE);
    frames.complete();
    await vi.advanceTimersByTimeAsync(READY_HOLD_MS - 1);
    expect(visit).not.toHaveBeenCalled();
    await vi.advanceTimersByTimeAsync(1);

    expect(shown()).toEqual(DONE);
    expect(visit).toHaveBeenCalledOnce();
  });

  it('keeps the modal up until the visit has landed, and answers as the visit did', async () => {
    const { frames, dialog, landing, gated } = await setup();
    frames.next(DONE);
    frames.complete();
    await vi.advanceTimersByTimeAsync(READY_HOLD_MS);
    expect(dialog.close).not.toHaveBeenCalled();

    landing.resolve(true);

    await expect(gated).resolves.toBe(true);
    expect(dialog.close).toHaveBeenCalledOnce();
  });

  it('visits at once, with no beat, when the stream fails', async () => {
    const { frames, visit } = await setup();

    frames.error(new Error('dropped'));
    await vi.advanceTimersByTimeAsync(0);

    expect(visit).toHaveBeenCalledOnce();
  });

  it('gives a silent stream up after the watchdog period', async () => {
    const { frames, visit } = await setup();
    frames.next(IMAGES);

    await vi.advanceTimersByTimeAsync(WATCHDOG_IDLE_MS - 1);
    expect(visit).not.toHaveBeenCalled();
    await vi.advanceTimersByTimeAsync(1);

    expect(visit).toHaveBeenCalledOnce();
    expect(frames.observed).toBe(false);
  });

  it('lets a visitor who dismisses the modal go at once, the stream dropped', async () => {
    const { frames, closed, visit } = await setup();
    frames.next(IMAGES);

    closed.next();
    await vi.advanceTimersByTimeAsync(0);

    expect(visit).toHaveBeenCalledOnce();
    expect(frames.observed).toBe(false);
  });
});
