import { signal } from '@angular/core';
import { type ComponentFixture, TestBed } from '@angular/core/testing';
import { TRANSLOCO_LOADER, TRANSLOCO_TRANSPILER, type Translation } from '@jsverse/transloco';
import { of } from 'rxjs';
import { PrecompiledTranspiler } from '../../../../../../../core/i18n/messages/precompiled-transpiler';
import type { PanelFailure } from '../../../../../state/panel-failure';
import { configureAdminTestBed } from '../../../../../testing/admin-test-bed';
import type { LivePoll } from '../poll/inject-live-poll';
import { LiveBadge } from './live-badge';

// The texts of the badge, compiled as the build ships them, to see them move.
const FRENCH: Translation = {
  state: { reasons: { server: ['HTTP ', ['arg', 'status']] } },
  prerender: {
    common: {
      live: {
        live: ['En direct · mis à jour ', ['arg', 'ago']],
        late: ['Mise à jour en retard : ', ['arg', 'ago']],
        compact_late: ['En retard · ', ['arg', 'ago']],
        compact_lost: ['Hors ligne · ', ['arg', 'ago']],
        lost: [
          'Hors ligne depuis ',
          ['arg', 'since'],
          ' (',
          ['arg', 'reason'],
          ') · nouvel essai ',
          ['arg', 'next'],
        ],
        hidden: "En pause tant que l'onglet est masqué",
        retry: 'Réessayer maintenant',
      },
      time: { ago: ['il y a ', ['arg', 'duration']], in: ['dans ', ['arg', 'duration']] },
    },
  },
};

const EVERY_MS = 5_000;

function fakePoll() {
  return {
    value: signal<unknown>(1),
    busy: signal(false),
    failure: signal<PanelFailure | null>(null),
    reload: vi.fn(),
    lastSuccessAt: signal<number | null>(Date.now() - 4_000),
    failures: signal(0),
    nextAttemptAt: signal<number | null>(null),
    isHidden: signal(false),
  } satisfies LivePoll<unknown>;
}

async function render(poll: LivePoll<unknown>, isCompact = false, announces = true) {
  const fixture = TestBed.createComponent(LiveBadge);
  fixture.componentRef.setInput('poll', poll);
  fixture.componentRef.setInput('every', EVERY_MS);
  fixture.componentRef.setInput('isCompact', isCompact);
  fixture.componentRef.setInput('announces', announces);
  await settle(fixture, 0);
  return fixture;
}

async function settle(fixture: ComponentFixture<LiveBadge>, ms: number): Promise<void> {
  await vi.advanceTimersByTimeAsync(ms);
  fixture.detectChanges();
  await fixture.whenStable();
  fixture.detectChanges();
}

function read(fixture: ComponentFixture<LiveBadge>, selector: string): string {
  const root = fixture.nativeElement as HTMLElement;
  return (root.querySelector(selector)?.textContent ?? '').replace(/\s+/g, ' ').trim();
}

describe('LiveBadge', () => {
  beforeEach(() => {
    vi.useFakeTimers();
    configureAdminTestBed(
      [],
      [
        { provide: TRANSLOCO_LOADER, useValue: { getTranslation: () => of(FRENCH) } },
        { provide: TRANSLOCO_TRANSPILER, useClass: PrecompiledTranspiler },
      ],
    );
  });

  afterEach(() => vi.useRealTimers());

  it('reads live, and moves its line every second without announcing it', async () => {
    const fixture = await render(fakePoll());

    expect(read(fixture, '[data-live-line]')).toBe('En direct · mis à jour il y a 4 s');
    expect(read(fixture, '[data-freshness]')).toBe('');
    await settle(fixture, 1_000);
    expect(read(fixture, '[data-live-line]')).toBe('En direct · mis à jour il y a 5 s');
    expect(read(fixture, '[aria-live]')).toBe('En direct · mis à jour il y a 4 s');
  });

  it('reads late past two cadences, and announces the change', async () => {
    const poll = fakePoll();
    const fixture = await render(poll);

    poll.lastSuccessAt.set(Date.now() - 30_000);
    await settle(fixture, 0);
    expect(read(fixture, '[data-live-line]')).toBe('Mise à jour en retard : il y a 30 s');
    expect(read(fixture, '[aria-live]')).toBe('Mise à jour en retard : il y a 30 s');
  });

  it('reads lost past a minute, with the reason, the next try and a way to try now', async () => {
    const poll = fakePoll();
    poll.lastSuccessAt.set(Date.now() - 120_000);
    poll.failure.set({ kind: 'server', status: 502 });
    poll.nextAttemptAt.set(Date.now() + 18_000);
    const fixture = await render(poll);

    expect(read(fixture, '[data-live-line]')).toBe(
      'Hors ligne depuis 2 min (HTTP 502) · nouvel essai dans 18 s',
    );
    (fixture.nativeElement as HTMLElement).querySelector('button')?.click();
    expect(poll.reload).toHaveBeenCalledOnce();
  });

  it('reads paused while the tab is hidden', async () => {
    const poll = fakePoll();
    poll.isHidden.set(true);
    const fixture = await render(poll);

    expect(read(fixture, '[data-live-line]')).toBe("En pause tant que l'onglet est masqué");
  });

  it('keeps the dot and the age alone when compact, the whole line in its title', async () => {
    const fixture = await render(fakePoll(), true);
    const line = (fixture.nativeElement as HTMLElement).querySelector('[data-live-line]');

    expect(read(fixture, '[data-live-line]')).toBe('il y a 4 s');
    expect(line?.getAttribute('title')).toBe('En direct · mis à jour il y a 4 s');
    expect((fixture.nativeElement as HTMLElement).querySelector('button')).toBeNull();
  });

  it('says late or offline in words when compact, not by the colour of its dot alone', async () => {
    const poll = fakePoll();
    poll.lastSuccessAt.set(Date.now() - 30_000);
    const fixture = await render(poll, true);
    expect(read(fixture, '[data-live-line]')).toBe('En retard · il y a 30 s');

    poll.lastSuccessAt.set(Date.now() - 120_000);
    await settle(fixture, 0);
    expect(read(fixture, '[data-live-line]')).toBe('Hors ligne · il y a 2 min');
  });

  it('leaves the announcement to another badge when asked', async () => {
    const fixture = await render(fakePoll(), true, false);

    expect((fixture.nativeElement as HTMLElement).querySelector('[aria-live]')).toBeNull();
  });

  it('shows nothing before the first answer', async () => {
    const poll = fakePoll();
    poll.lastSuccessAt.set(null);
    const fixture = await render(poll);

    expect((fixture.nativeElement as HTMLElement).querySelector('[data-freshness]')).toBeNull();
  });
});
