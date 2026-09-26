import { Directionality } from '@angular/cdk/bidi';
import { Component, signal } from '@angular/core';
import { type ComponentFixture, TestBed } from '@angular/core/testing';
import { provideTransloco } from '@jsverse/transloco';
import { of } from 'rxjs';
import type { ChampionAbility } from '../../../../../core/api/generated/models/champion-ability';
import { abilityChipsOf } from './ability-chips-of';
import { abilityKey } from './ability-key';
import { AbilityShowcase } from './ability-showcase';

function clipOf(slot: string) {
  return {
    webm: `https://cdn.test/${slot}.webm`,
    mp4: `https://cdn.test/${slot}.mp4`,
    poster: `https://cdn.test/${slot}.jpg`,
  };
}

function abilityOf(overrides: Partial<ChampionAbility>): ChampionAbility {
  return {
    slot: 'q',
    name: 'Disintegrate',
    description: 'Deals <magicDamage>magic damage</magicDamage>.',
    image: { status: 'absent' },
    ...overrides,
  };
}

const KIT: ChampionAbility[] = [
  abilityOf({ slot: 'passive', name: 'Pyromania', video: clipOf('p') }),
  abilityOf({
    slot: 'q',
    name: 'Disintegrate',
    cooldown: '4',
    cost: '60',
    maxRank: 5,
    video: clipOf('q'),
  }),
  abilityOf({ slot: 'w', name: 'Incinerate', cost: '0', maxRank: 5 }),
  abilityOf({ slot: 'e', name: 'Molten Shield', video: clipOf('e') }),
  abilityOf({ slot: 'r', name: 'Summon: Tibbers', charges: 1, maxRank: 3, video: clipOf('r') }),
];

describe('abilityKey', () => {
  it('labels the passive P and a spell by its slot', () => {
    expect(abilityKey('passive')).toBe('P');
    expect(abilityKey('r')).toBe('R');
  });
});

describe('abilityChipsOf', () => {
  it('lists the cast figures in order, the cost in the champion resource', () => {
    const chips = abilityChipsOf(
      abilityOf({ cooldown: '4', cost: '60', range: '625', charges: 2, maxRank: 5 }),
      'Mana',
    );
    expect(chips).toEqual([
      { label: 'champion.detail.cooldown', value: '4', unit: 's' },
      { label: 'champion.detail.cost', value: '60', unit: 'Mana' },
      { label: 'champion.detail.range', value: '625', unit: '' },
      { label: 'champion.detail.charges', value: '2', unit: '' },
      { label: 'champion.detail.ranks', value: '5', unit: '' },
    ]);
  });

  it('hides the cost of a free ability and the ranks of the passive', () => {
    const labels = (ability: ChampionAbility) =>
      abilityChipsOf(ability, 'Mana').map((chip) => chip.label);
    expect(labels(abilityOf({ cost: '0', maxRank: 5 }))).toEqual(['champion.detail.ranks']);
    expect(labels(abilityOf({ slot: 'passive', cooldown: '', maxRank: 1 }))).toEqual([]);
  });
});

@Component({
  imports: [AbilityShowcase],
  template: '<lodb-ability-showcase [abilities]="abilities()" resource="Mana" />',
})
class Host {
  readonly abilities = signal(KIT);
}

describe('lodb-ability-showcase', () => {
  let reducedMotion: boolean;
  const direction = { value: 'ltr' as 'ltr' | 'rtl' };

  beforeEach(() => {
    reducedMotion = false;
    direction.value = 'ltr';
    Object.defineProperty(document.defaultView, 'matchMedia', {
      configurable: true,
      value: () => ({ matches: reducedMotion }),
    });
    // jsdom plays no media: the element methods are observed instead.
    vi.spyOn(HTMLMediaElement.prototype, 'pause').mockImplementation(() => undefined);
    vi.spyOn(HTMLMediaElement.prototype, 'play').mockResolvedValue(undefined);
    TestBed.configureTestingModule({
      providers: [
        { provide: Directionality, useValue: direction },
        provideTransloco({
          config: { defaultLang: 'en', missingHandler: { logMissingKey: false }, prodMode: true },
          loader: class {
            getTranslation = () => of({});
          },
        }),
      ],
    });
  });

  afterEach(() => {
    // The components go before the spies do: leaving pauses their video.
    TestBed.resetTestingModule();
    Reflect.deleteProperty(document.defaultView ?? {}, 'matchMedia');
    vi.restoreAllMocks();
  });

  const pauses = () => vi.mocked(HTMLMediaElement.prototype.pause);

  async function render(): Promise<ComponentFixture<Host>> {
    const fixture = TestBed.createComponent(Host);
    await fixture.whenStable();
    return fixture;
  }

  function tabs(fixture: ComponentFixture<Host>): HTMLButtonElement[] {
    return [
      ...(fixture.nativeElement as HTMLElement).querySelectorAll<HTMLButtonElement>('[role="tab"]'),
    ];
  }

  function stage(fixture: ComponentFixture<Host>): HTMLElement {
    return (fixture.nativeElement as HTMLElement).querySelector(
      'lodb-ability-media',
    ) as HTMLElement;
  }

  async function press(fixture: ComponentFixture<Host>, key: string): Promise<void> {
    const event = new KeyboardEvent('keydown', { key, bubbles: true, cancelable: true });
    tabs(fixture)[0]?.dispatchEvent(event);
    await fixture.whenStable();
  }

  const selectedIndex = (fixture: ComponentFixture<Host>) =>
    tabs(fixture).findIndex((tab) => tab.getAttribute('aria-selected') === 'true');

  it('renders a tab per ability and every description, only the selected one shown', async () => {
    const fixture = await render();
    const element = fixture.nativeElement as HTMLElement;
    const panels = [...element.querySelectorAll<HTMLElement>('[role="tabpanel"]')];

    expect(tabs(fixture).map((tab) => tab.querySelector('.tab__key')?.textContent?.trim())).toEqual(
      ['P', 'Q', 'W', 'E', 'R'],
    );
    expect(tabs(fixture)[0]?.getAttribute('aria-label')).toBe('P — Pyromania');
    expect(tabs(fixture).map((tab) => tab.tabIndex)).toEqual([0, -1, -1, -1, -1]);
    expect(panels.map((panel) => panel.hidden)).toEqual([false, true, true, true, true]);
    expect(panels[1]?.querySelector('magicdamage')?.textContent).toBe('magic damage');
    expect(panels[0]?.getAttribute('aria-labelledby')).toBe(tabs(fixture)[0]?.id);
  });

  it('selects an ability on click and shows its figures', async () => {
    const fixture = await render();

    tabs(fixture)[1]?.click();
    await fixture.whenStable();

    const panel = (fixture.nativeElement as HTMLElement).querySelector(
      '[role="tabpanel"]:not([hidden])',
    );
    expect(selectedIndex(fixture)).toBe(1);
    expect(panel?.id).toBe('ability-panel-q');
    expect(panel?.querySelectorAll('.figure')).toHaveLength(3);
  });

  it('moves along the rail with the arrows, wrapping, and focuses the new tab', async () => {
    const fixture = await render();

    await press(fixture, 'ArrowLeft');
    expect(selectedIndex(fixture)).toBe(4);
    expect(document.activeElement).toBe(tabs(fixture)[4]);

    await press(fixture, 'Home');
    await press(fixture, 'ArrowRight');
    expect(selectedIndex(fixture)).toBe(1);

    await press(fixture, 'End');
    expect(selectedIndex(fixture)).toBe(4);
  });

  it('follows the reading direction in a right-to-left page', async () => {
    direction.value = 'rtl';
    const fixture = await render();

    await press(fixture, 'ArrowLeft');
    expect(selectedIndex(fixture)).toBe(1);
  });

  it('plays the selected clip muted once rendered in the browser', async () => {
    const fixture = await render();
    const video = stage(fixture).querySelector('video');

    expect(video?.muted).toBe(true);
    expect(video?.autoplay).toBe(true);
    expect(video?.querySelector('source')?.getAttribute('src')).toBe('https://cdn.test/p.webm');
    expect(stage(fixture).querySelector('.sound')?.getAttribute('aria-pressed')).toBe('false');
  });

  it('lets the reader turn the sound on, for the next clips too', async () => {
    const fixture = await render();

    stage(fixture).querySelector<HTMLButtonElement>('.sound')?.click();
    await fixture.whenStable();
    tabs(fixture)[1]?.click();
    await fixture.whenStable();

    const video = stage(fixture).querySelector('video');
    expect(video?.querySelector('source')?.getAttribute('src')).toBe('https://cdn.test/q.webm');
    expect(video?.muted).toBe(false);
    expect(pauses()).toHaveBeenCalled();
  });

  it('shows the poster, not the video, to a reader who asked for reduced motion', async () => {
    reducedMotion = true;
    const fixture = await render();

    expect(stage(fixture).querySelector('video')).toBeNull();
    expect(stage(fixture).querySelector('img.poster')?.getAttribute('src')).toBe(
      'https://cdn.test/p.jpg',
    );
  });

  it('shows the icon of an ability without a clip', async () => {
    const fixture = await render();

    tabs(fixture)[2]?.click();
    await fixture.whenStable();

    expect(stage(fixture).classList).toContain('idle');
    expect(stage(fixture).querySelector('video')).toBeNull();
    expect(stage(fixture).querySelector('lodb-catalogue-image.icon')).not.toBeNull();
  });

  it('falls back to the icon when the clip fails to load, for that ability only', async () => {
    const fixture = await render();

    stage(fixture).querySelector('source[type="video/mp4"]')?.dispatchEvent(new Event('error'));
    await fixture.whenStable();
    expect(stage(fixture).classList).toContain('idle');
    expect(stage(fixture).querySelector('video')).toBeNull();

    tabs(fixture)[1]?.click();
    await fixture.whenStable();
    expect(stage(fixture).querySelector('video')).not.toBeNull();
  });

  it('stops the video when the reader leaves the page', async () => {
    const fixture = await render();
    pauses().mockClear();

    fixture.destroy();

    expect(pauses()).toHaveBeenCalled();
  });

  it('starts over on the first ability of another champion', async () => {
    const fixture = await render();
    tabs(fixture)[3]?.click();
    await fixture.whenStable();

    fixture.componentInstance.abilities.set(KIT.map((ability) => ({ ...ability })));
    await fixture.whenStable();

    expect(selectedIndex(fixture)).toBe(0);
  });
});
