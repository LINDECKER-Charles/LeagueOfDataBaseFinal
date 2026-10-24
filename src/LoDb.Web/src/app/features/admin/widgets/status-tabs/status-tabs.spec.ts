import { Directionality } from '@angular/cdk/bidi';
import { ChangeDetectionStrategy, Component, signal } from '@angular/core';
import { type ComponentFixture, TestBed } from '@angular/core/testing';
import { Tab } from '../../../../ui/tabs/tab';
import { StatusTabs } from './status-tabs';
import type { StatusTile } from './status-tile';

const TILES: readonly StatusTile[] = [
  { key: 'pass', label: 'Passe', value: '71 %', sub: 'n°12 · epoch 2', tone: 'live' },
  { key: 'coverage', label: 'Couverture', value: '97,4 %', sub: null, tone: null },
  { key: 'history', label: 'Historique', value: 'n°12', sub: 'terminée', tone: 'bad' },
];

@Component({
  imports: [StatusTabs, Tab],
  template: `
    <lodb-status-tabs label="Vues" [tiles]="tiles" [(selected)]="selected">
      <lodb-tab key="pass" label="Passe">pass body</lodb-tab>
      <lodb-tab key="coverage" label="Couverture">coverage body</lodb-tab>
      <lodb-tab key="history" label="Historique">history body</lodb-tab>
    </lodb-status-tabs>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
class StatusTabsHost {
  readonly tiles = TILES;
  readonly selected = signal<string | null>(null);
}

type Fixture = ComponentFixture<StatusTabsHost>;

function page(fixture: Fixture): HTMLElement {
  return fixture.nativeElement as HTMLElement;
}

function tabs(fixture: Fixture): HTMLButtonElement[] {
  return [...page(fixture).querySelectorAll<HTMLButtonElement>('[role="tab"]')];
}

function panel(fixture: Fixture, key: string): HTMLElement {
  return page(fixture).querySelector(`#${key}-panel`) as HTMLElement;
}

function words(element: Element | null): string {
  return (element?.textContent ?? '').replace(/\s+/g, ' ').trim();
}

// A tile as read: its name, its figure and its line, one after the other.
function tileWords(tab: Element): string {
  return ['[data-tile-name]', '[data-tile-value]', '[data-tile-sub]']
    .map((part) => words(tab.querySelector(part)))
    .filter((text) => text !== '')
    .join(' ');
}

describe('StatusTabs', () => {
  const direction = signal<'ltr' | 'rtl'>('ltr');

  beforeEach(() => {
    direction.set('ltr');
    TestBed.configureTestingModule({
      providers: [{ provide: Directionality, useValue: { valueSignal: direction } }],
    });
  });

  async function render(selected: string | null = null): Promise<Fixture> {
    const fixture = TestBed.createComponent(StatusTabsHost);
    fixture.componentInstance.selected.set(selected);
    await fixture.whenStable();
    return fixture;
  }

  it('shows each view as its figure, the first one selected and its panel alone shown', async () => {
    const fixture = await render();

    expect(tabs(fixture).map(tileWords)).toEqual([
      'Passe 71 % n°12 · epoch 2',
      'Couverture 97,4 %',
      'Historique n°12 terminée',
    ]);
    expect(tabs(fixture).map((tab) => tab.getAttribute('aria-selected'))).toEqual([
      'true',
      'false',
      'false',
    ]);
    expect(panel(fixture, 'pass').hidden).toBe(false);
    expect(panel(fixture, 'history').hidden).toBe(true);
    expect(words(panel(fixture, 'history'))).toBe('history body');
  });

  it('names a tab by its view, describes it by its figure and links it to its panel', async () => {
    const fixture = await render();
    const [first] = tabs(fixture);

    expect(words(page(fixture).querySelector(`#${first.getAttribute('aria-labelledby')}`))).toBe(
      'Passe',
    );
    const state = page(fixture).querySelector(`#${first.getAttribute('aria-describedby')}`);
    expect(state?.querySelector('[data-tile-value]')?.textContent).toBe('71 %');
    expect(state?.querySelector('[data-tile-sub]')?.textContent).toBe('n°12 · epoch 2');
    expect(first.getAttribute('aria-controls')).toBe('pass-panel');
    expect(panel(fixture, 'pass').getAttribute('aria-labelledby')).toBe(first.id);
  });

  it('marks the views that have something to say, and those alone', async () => {
    const fixture = await render();

    expect(
      tabs(fixture).map((tab) => tab.querySelector('.dot')?.getAttribute('data-tone')),
    ).toEqual(['live', undefined, 'bad']);
  });

  it('opens the view asked for, and the first one for a view it does not have', async () => {
    expect(panel(await render('coverage'), 'coverage').hidden).toBe(false);
    expect(panel(await render('nowhere'), 'pass').hidden).toBe(false);
  });

  it('selects a view on click and hands its key back', async () => {
    const fixture = await render();

    tabs(fixture)[2].click();
    await fixture.whenStable();

    expect(fixture.componentInstance.selected()).toBe('history');
    expect(panel(fixture, 'history').hidden).toBe(false);
    expect(panel(fixture, 'pass').hidden).toBe(true);
  });

  it('moves the selection and the focus with the keys, the arrows mirrored right to left', async () => {
    direction.set('rtl');
    const fixture = await render();

    tabs(fixture)[0].dispatchEvent(new KeyboardEvent('keydown', { key: 'ArrowLeft' }));
    await fixture.whenStable();
    expect(fixture.componentInstance.selected()).toBe('coverage');
    expect(document.activeElement).toBe(tabs(fixture)[1]);
    expect(tabs(fixture).map((tab) => tab.tabIndex)).toEqual([-1, 0, -1]);

    tabs(fixture)[1].dispatchEvent(new KeyboardEvent('keydown', { key: 'End' }));
    await fixture.whenStable();
    expect(fixture.componentInstance.selected()).toBe('history');
  });
});
