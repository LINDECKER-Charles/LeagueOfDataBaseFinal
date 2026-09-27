import { Component, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideTransloco } from '@jsverse/transloco';
import { of } from 'rxjs';
import type { ChampionCard } from '../../../../../core/api/generated/models/champion-card';
import type { ChampionStat } from '../../../../../core/api/generated/models/champion-stat';
import { formatStat } from './format-stat';
import { growthFactor } from './growth-factor';
import { StatBoard } from './stat-board';
import { statAt } from './stat-at';
import type { StatRow } from './stat-row';
import { statRowsOf } from './stat-rows-of';

const STATS: ChampionStat[] = [
  { stat: 'health', base: 560, perLevel: 96 },
  { stat: 'health_regen', base: 5.5, perLevel: 0.55 },
  { stat: 'mana', base: 418, perLevel: 25 },
  { stat: 'mana_regen', base: 8, perLevel: 0.8 },
  { stat: 'attack_damage', base: 50, perLevel: 2.625 },
  { stat: 'attack_speed', base: 0.625, perLevel: 2.5 },
  { stat: 'armor', base: 23, perLevel: 4 },
  { stat: 'magic_resist', base: 30, perLevel: 1.3 },
  { stat: 'move_speed', base: 335, perLevel: null },
  { stat: 'attack_range', base: 625, perLevel: null },
];

function cardOf(overrides: Partial<ChampionCard> = {}): ChampionCard {
  return {
    canonicalPath: 'champions/Annie',
    id: 'Annie',
    key: '1',
    name: 'Annie',
    title: 'the Dark Child',
    image: { status: 'absent' },
    loadingArt: 'https://ddragon.leagueoflegends.com/cdn/img/champion/loading/Annie_0.jpg',
    blurb: 'Dangerous, yet disarmingly precocious.',
    partype: 'Mana',
    resource: 'mana',
    stats: STATS,
    tags: ['Mage'],
    ...overrides,
  };
}

function rowOf(overrides: Partial<StatRow>): StatRow {
  return {
    stat: 'health',
    label: 'stat.health',
    text: null,
    base: 0,
    growth: 0,
    kind: 'flat',
    ...overrides,
  };
}

describe('growthFactor', () => {
  it('follows the game curve: nothing at level 1, the whole gain 17 times at level 18', () => {
    expect(growthFactor(1)).toBe(0);
    expect(growthFactor(2)).toBeCloseTo(0.72, 10);
    expect(growthFactor(18)).toBeCloseTo(17, 10);
  });

  it('weighs the late levels more than the early ones', () => {
    const early = growthFactor(3) - growthFactor(2);
    const late = growthFactor(18) - growthFactor(17);
    expect(late).toBeGreaterThan(early);
  });
});

describe('statAt', () => {
  it('adds the flat gain along the curve: g × (n − 1) × (0.7025 + 0.0175 × (n − 1))', () => {
    const health = rowOf({ base: 560, growth: 96 });
    expect(statAt(health, 1)).toBe(560);
    expect(statAt(health, 2)).toBeCloseTo(560 + 96 * 0.72, 10);
    expect(statAt(health, 18)).toBeCloseTo(2192, 10);
  });

  it('grows the attack speed by a percentage of its base', () => {
    const speed = rowOf({ stat: 'attack_speed', base: 0.625, growth: 2.5, kind: 'percent' });
    expect(statAt(speed, 1)).toBe(0.625);
    expect(statAt(speed, 18)).toBeCloseTo(0.625 * (1 + 0.025 * 17), 10);
  });

  it('keeps a static stat whatever the level', () => {
    const range = rowOf({ stat: 'attack_range', base: 625, growth: 99, kind: 'static' });
    expect(statAt(range, 18)).toBe(625);
  });
});

describe('formatStat', () => {
  it('keeps two decimals below 10, one below 1000, none above', () => {
    expect(formatStat(0.890625)).toBe('0.89');
    expect(formatStat(5.5)).toBe('5.5');
    expect(formatStat(629.12)).toBe('629.1');
    expect(formatStat(2192.4)).toBe('2192');
    expect(formatStat(335)).toBe('335');
  });
});

describe('statRowsOf', () => {
  it('orders offence, survival, the resource, then the static stats', () => {
    expect(statRowsOf(cardOf()).map((row) => row.stat)).toEqual([
      'attack_damage',
      'attack_speed',
      'health',
      'health_regen',
      'armor',
      'magic_resist',
      'mana',
      'mana_regen',
      'move_speed',
      'attack_range',
    ]);
  });

  it('names the resource rows after the champion resource', () => {
    const rows = statRowsOf(cardOf({ partype: 'Energy' }));
    const mana = rows.find((row) => row.stat === 'mana');
    const regen = rows.find((row) => row.stat === 'mana_regen');
    expect(mana?.text).toBe('Energy');
    expect(regen).toMatchObject({ label: 'champion.detail.stats.resource_regen', text: null });
  });

  it('leaves out the resource rows of a champion without a named, pooled resource', () => {
    const noName = statRowsOf(cardOf({ partype: '' })).map((row) => row.stat);
    const noPool = cardOf({
      stats: STATS.map((stat) => (stat.stat === 'mana' ? { ...stat, base: 0 } : stat)),
    });
    expect(noName).not.toContain('mana');
    expect(statRowsOf(noPool).map((row) => row.stat)).not.toContain('mana_regen');
  });

  it('marks the attack speed as a percentage and the move speed and range as static', () => {
    const kinds = Object.fromEntries(statRowsOf(cardOf()).map((row) => [row.stat, row.kind]));
    expect(kinds).toMatchObject({
      attack_speed: 'percent',
      health: 'flat',
      move_speed: 'static',
      attack_range: 'static',
    });
  });

  it('reads a stat the API does not give as zero, without growth', () => {
    const rows = statRowsOf(cardOf({ stats: [] }));
    expect(rows.find((row) => row.stat === 'armor')).toMatchObject({ base: 0, growth: 0 });
  });
});

@Component({
  imports: [StatBoard],
  template: '<lodb-stat-board [profile]="profile()" version="16.19.1" />',
})
class Host {
  readonly profile = signal(cardOf());
}

describe('lodb-stat-board', () => {
  function render() {
    TestBed.configureTestingModule({
      providers: [
        provideTransloco({
          config: { defaultLang: 'en', missingHandler: { logMissingKey: false }, prodMode: true },
          loader: class {
            getTranslation = () => of({});
          },
        }),
      ],
    });
    const fixture = TestBed.createComponent(Host);
    fixture.detectChanges();
    const element = fixture.nativeElement as HTMLElement;
    const valueOf = (index: number) =>
      element.querySelectorAll('.row__value')[index]?.textContent?.trim();
    const slider = element.querySelector('input[type="range"]') as HTMLInputElement;
    return { fixture, element, valueOf, slider };
  }

  function slide(slider: HTMLInputElement, level: number): void {
    slider.value = String(level);
    slider.dispatchEvent(new Event('input'));
  }

  it('renders level 1 with the per-level gains beside the values', () => {
    const { element, valueOf } = render();
    expect(valueOf(0)).toBe('50');
    expect(valueOf(1)).toBe('0.63');
    const growths = [...element.querySelectorAll('.row__growth')].map((node) => node.textContent);
    expect(growths.slice(0, 2)).toEqual(['+2.625', '+2.5%']);
  });

  it('scales every row to the level the slider picks', async () => {
    const { fixture, valueOf, slider } = render();
    slide(slider, 18);
    await fixture.whenStable();
    expect(valueOf(0)).toBe('94.6');
    expect(valueOf(1)).toBe('0.89');
    expect(valueOf(2)).toBe('2192');
    expect(valueOf(8)).toBe('335');
  });

  it('starts over at level 1 on another champion', async () => {
    const { fixture, valueOf, slider } = render();
    slide(slider, 18);
    await fixture.whenStable();
    fixture.componentInstance.profile.set(cardOf({ id: 'Brand', name: 'Brand' }));
    await fixture.whenStable();
    expect(valueOf(2)).toBe('560');
    expect(slider.value).toBe('1');
  });
});
