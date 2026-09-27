import { Component, type Type, signal } from '@angular/core';
import { type ComponentFixture, TestBed } from '@angular/core/testing';
import { configureAdminTestBed } from '../testing/admin-test-bed';
import { AdminPager } from './admin-pager';
import { Badge, type Tone } from './badge';
import { ConfirmButton } from './confirm-button';
import { Kpi } from './kpi';
import { Legend } from './legend';
import { RankList } from './rank-list';
import { SegmentBar } from './segment-bar';

function render<T>(type: Type<T>, inputs: Readonly<Record<string, unknown>>): ComponentFixture<T> {
  const fixture = TestBed.createComponent(type);
  for (const [name, value] of Object.entries(inputs)) {
    fixture.componentRef.setInput(name, value);
  }
  fixture.detectChanges();
  return fixture;
}

function root(fixture: ComponentFixture<unknown>): HTMLElement {
  return fixture.nativeElement as HTMLElement;
}

function all(fixture: ComponentFixture<unknown>, selector: string): HTMLElement[] {
  return [...root(fixture).querySelectorAll<HTMLElement>(selector)];
}

function words(element: Element | null | undefined): string {
  return (element?.textContent ?? '').replace(/\s+/g, ' ').trim();
}

@Component({
  imports: [Badge],
  template: `<span [lodbBadge]="tone()">x</span>`,
})
class BadgeHost {
  readonly tone = signal<Tone | ''>('bad');
}

describe('admin widgets', () => {
  beforeEach(() => configureAdminTestBed());

  it('shows a key figure with its label, its value, its line and the bar of its accent', () => {
    const fixture = render(Kpi, { label: 'Vues', value: '1 234', sub: '+5 %', accent: 'bad' });

    expect(words(root(fixture))).toBe('Vues 1 234 +5 %');
    const bar = root(fixture).querySelector<HTMLElement>('[aria-hidden="true"]');
    expect(bar?.style.background).toBe('var(--color-bad)');
  });

  it('leaves the value out of a tile of badges, and reads its line in cyan as a link', () => {
    const fixture = render(Kpi, { label: 'Services', sub: 'Surveillance →', isLink: true });

    expect(words(root(fixture))).toBe('Services Surveillance →');
    expect(all(fixture, '.text-hex').map(words)).toEqual(['Surveillance →']);
  });

  it('dresses a badge in its tone, and a blank tone as a neutral one', () => {
    const fixture = TestBed.createComponent(BadgeHost);
    fixture.detectChanges();
    const badge = () => root(fixture).querySelector('span');
    expect(badge()?.className).toContain('text-danger-light');
    expect(badge()?.className).not.toContain('uppercase');

    fixture.componentInstance.tone.set('');
    fixture.detectChanges();
    expect(badge()?.className).toContain('text-text-muted');
  });

  it('sizes each bar of a ranking on its largest row', () => {
    const fixture = render(RankList, {
      rows: [
        { name: 'home', value: 200 },
        { name: 'items', value: 50 },
      ],
      format: 'int',
    });

    expect(all(fixture, 'li span[style]').map((bar) => bar.style.inlineSize)).toEqual([
      '100%',
      '25%',
    ]);
    expect(words(all(fixture, 'li')[0])).toBe('home 200');
  });

  it('folds a ranking past its limit behind a toggle', () => {
    const rows = ['a', 'b', 'c', 'd'].map((name, index) => ({ name, value: 10 - index }));
    const fixture = render(RankList, { rows, limit: 2 });

    expect(all(fixture, 'li')).toHaveLength(2);
    const toggle = root(fixture).querySelector<HTMLButtonElement>('button');
    expect(toggle?.getAttribute('aria-expanded')).toBe('false');
    expect(words(toggle)).toBe('admin.rank.more');

    toggle?.click();
    fixture.detectChanges();
    expect(all(fixture, 'li')).toHaveLength(4);
    expect(words(root(fixture).querySelector('button'))).toBe('admin.rank.less');
  });

  it('says what an empty ranking holds instead of drawing it', () => {
    const fixture = render(RankList, { rows: [], empty: 'admin.users.empty' });

    expect(words(root(fixture))).toBe('admin.users.empty');
    expect(all(fixture, 'ol')).toHaveLength(0);
  });

  it('pages a list through the URL, and hides the pager of a single page', () => {
    expect(all(render(AdminPager, { page: 1, pages: 1 }), 'nav')).toHaveLength(0);

    const fixture = render(AdminPager, { page: 2, pages: 3 });

    const links = all(fixture, 'a').map((link) => link.getAttribute('href'));
    expect(links).toEqual(['/?page=1', '/?page=3']);
    expect(words(root(fixture).querySelector('nav span'))).toBe('2 / 3');
  });

  it('keeps the direction that leads nowhere in sight, greyed out', () => {
    const fixture = render(AdminPager, { page: 1, pages: 2 });

    expect(all(fixture, 'a').map((link) => link.getAttribute('href'))).toEqual(['/?page=2']);
    const disabled = all(fixture, '[aria-disabled="true"]');
    expect(disabled.map(words)).toEqual(['← admin.pager.previous']);
  });

  it('pages a journal read by cursor while the API says more follows', () => {
    const fixture = render(AdminPager, { page: 1, hasMore: true });

    expect(all(fixture, 'a').map((link) => link.getAttribute('href'))).toEqual(['/?page=2']);
    expect(words(root(fixture).querySelector('nav span:not([aria-disabled])'))).toBe(
      'admin.pager.page',
    );

    fixture.componentRef.setInput('hasMore', false);
    fixture.detectChanges();
    expect(all(fixture, 'nav')).toHaveLength(0);
  });

  it('asks for a confirmation before it runs an action', async () => {
    const fixture = render(ConfirmButton, {
      label: 'Supprimer',
      confirmLabel: 'Confirmer',
      tone: 'danger',
      confirmTone: 'danger',
    });
    const confirmed = vi.fn();
    fixture.componentInstance.confirmed.subscribe(confirmed);
    expect(root(fixture).querySelector('button')?.className).toContain('hx-btn-danger');
    expect(root(fixture).querySelector('button')?.className).toContain('hx-btn-sm');

    root(fixture).querySelector('button')?.click();
    fixture.detectChanges();
    await fixture.whenStable();
    const [confirm, cancel] = all(fixture, 'button');
    expect(words(confirm)).toBe('Confirmer');
    expect(confirm?.className).toContain('hx-btn-danger');
    expect(document.activeElement).toBe(confirm);
    expect(confirmed).not.toHaveBeenCalled();

    cancel?.click();
    fixture.detectChanges();
    expect(words(root(fixture))).toBe('Supprimer');

    root(fixture).querySelector('button')?.click();
    fixture.detectChanges();
    all(fixture, 'button')[0]?.click();
    fixture.detectChanges();
    expect(confirmed).toHaveBeenCalledTimes(1);
    expect(words(root(fixture))).toBe('Supprimer');
  });

  it('keeps a busy action from being run again', () => {
    const fixture = render(ConfirmButton, { label: 'Révoquer', confirmLabel: 'Oui', busy: true });

    expect(root(fixture).querySelector('button')?.disabled).toBe(true);
  });

  it('marks the current choice of a segmented bar and links the others from the first page', () => {
    const fixture = render(SegmentBar, {
      param: 'range',
      label: 'admin.range.label',
      current: '90d',
      segments: [
        { value: '30d', label: 'admin.range.30d', name: 'admin.range_long.30d' },
        { value: '90d', label: 'admin.range.90d', name: 'admin.range_long.90d' },
        { value: '', label: 'admin.contacts.statuses.all' },
      ],
    });

    const links = all(fixture, 'a');
    expect(links.map((link) => link.getAttribute('href'))).toEqual([
      '/?range=30d',
      '/?range=90d',
      '/',
    ]);
    expect(links.map((link) => link.getAttribute('aria-label'))).toEqual([
      'admin.range_long.30d',
      'admin.range_long.90d',
      null,
    ]);
    expect(links.filter((link) => link.getAttribute('aria-current') === 'page')).toEqual([
      links[1],
    ]);
  });

  it('names each series of a legend with a diamond, and the count of a slice', () => {
    const fixture = render(Legend, {
      items: [
        { label: 'Vues', color: 'var(--color-gold)' },
        { label: 'Mobile', color: 'var(--color-hex)', value: '40' },
      ],
    });

    expect(all(fixture, 'li').map(words)).toEqual(['Vues', 'Mobile 40']);
    const swatch = all(fixture, 'li > span')[1];
    expect(swatch?.style.background).toBe('var(--color-hex)');
    expect(swatch?.className).toContain('rotate-45');
  });
});
