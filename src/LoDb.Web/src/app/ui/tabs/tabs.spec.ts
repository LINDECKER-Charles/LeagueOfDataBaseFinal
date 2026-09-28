import { Directionality } from '@angular/cdk/bidi';
import { ChangeDetectionStrategy, Component, signal } from '@angular/core';
import { type ComponentFixture, TestBed } from '@angular/core/testing';
import { tabIndexAfter } from './tab-index-after';
import { tabMoveForKey } from './tab-move-for-key';
import { Tab } from './tab';
import { Tabs } from './tabs';

describe('tabMoveForKey', () => {
  it.each([
    ['ArrowRight', 'ltr', 'next'],
    ['ArrowLeft', 'ltr', 'previous'],
    ['ArrowLeft', 'rtl', 'next'],
    ['ArrowRight', 'rtl', 'previous'],
    ['Home', 'rtl', 'first'],
    ['End', 'ltr', 'last'],
    ['Enter', 'ltr', null],
  ] as const)('reads %s in a %s page as %s', (key, direction, expected) => {
    expect(tabMoveForKey(key, direction)).toBe(expected);
  });
});

describe('tabIndexAfter', () => {
  it.each([
    ['next', 1, 2],
    ['next', 3, 0],
    ['previous', 0, 3],
    ['previous', 2, 1],
    ['first', 2, 0],
    ['last', 0, 3],
  ] as const)('moves %s from %i to %i in four tabs', (move, index, expected) => {
    expect(tabIndexAfter(move, index, 4)).toBe(expected);
  });
});

@Component({
  imports: [Tab, Tabs],
  template: `
    <lodb-tabs label="Champion">
      <lodb-tab key="stats" label="Stats">stats body</lodb-tab>
      <lodb-tab key="spells" label="Spells">spells body</lodb-tab>
      <lodb-tab key="skins" label="Skins">skins body</lodb-tab>
    </lodb-tabs>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
class TabsHost {}

describe('Tabs', () => {
  const direction = signal<'ltr' | 'rtl'>('ltr');

  beforeEach(() => {
    direction.set('ltr');
    TestBed.configureTestingModule({
      providers: [{ provide: Directionality, useValue: { valueSignal: direction } }],
    });
  });

  async function render(): Promise<ComponentFixture<TabsHost>> {
    const fixture = TestBed.createComponent(TabsHost);
    await fixture.whenStable();
    return fixture;
  }

  function tabs(fixture: ComponentFixture<TabsHost>): HTMLButtonElement[] {
    return Array.from((fixture.nativeElement as HTMLElement).querySelectorAll('[role=tab]'));
  }

  function panel(fixture: ComponentFixture<TabsHost>, key: string): HTMLElement {
    return (fixture.nativeElement as HTMLElement).querySelector(`#${key}-panel`) as HTMLElement;
  }

  it('selects the first tab and keeps every panel in the DOM', async () => {
    const fixture = await render();

    expect(tabs(fixture).map((tab) => tab.getAttribute('aria-selected'))).toEqual([
      'true',
      'false',
      'false',
    ]);
    expect(panel(fixture, 'stats').hidden).toBe(false);
    expect(panel(fixture, 'skins').hidden).toBe(true);
    expect(panel(fixture, 'skins').textContent).toContain('skins body');
  });

  it('links each tab to its panel', async () => {
    const fixture = await render();
    const [first] = tabs(fixture);

    expect(first.getAttribute('aria-controls')).toBe('stats-panel');
    expect(panel(fixture, 'stats').getAttribute('aria-labelledby')).toBe(first.id);
  });

  it('selects a tab on click', async () => {
    const fixture = await render();

    tabs(fixture)[1].click();
    await fixture.whenStable();

    expect(panel(fixture, 'spells').hidden).toBe(false);
    expect(panel(fixture, 'stats').hidden).toBe(true);
  });

  it('moves selection and focus with the mirrored arrows of a right-to-left page', async () => {
    direction.set('rtl');
    const fixture = await render();

    tabs(fixture)[0].dispatchEvent(new KeyboardEvent('keydown', { key: 'ArrowLeft' }));
    await fixture.whenStable();

    expect(tabs(fixture)[1].getAttribute('aria-selected')).toBe('true');
    expect(document.activeElement).toBe(tabs(fixture)[1]);
    expect(tabs(fixture).map((tab) => tab.tabIndex)).toEqual([-1, 0, -1]);
  });
});
