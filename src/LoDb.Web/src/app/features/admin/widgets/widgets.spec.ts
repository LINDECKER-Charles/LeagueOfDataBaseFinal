import { Component, type Type, signal } from '@angular/core';
import { type ComponentFixture, TestBed } from '@angular/core/testing';
import { Seo } from '../../../core/seo/seo';
import { configureAdminTestBed } from '../testing/admin-test-bed';
import { AdminCard } from './admin-card';
import { AdminPager } from './admin-pager';
import { Badge, type Tone } from './badge';
import { ConfirmButton } from './confirm-button';
import { Kpi } from './kpi';
import { Legend } from './legend';
import { PageHead } from './page-head';
import { RangeBar } from './range-bar';
import { RankList } from './rank-list';

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
  const apply = vi.fn(() => Promise.resolve());

  beforeEach(() => {
    apply.mockClear();
    configureAdminTestBed([], [{ provide: Seo, useValue: { apply } }]);
  });

  it('shows a key figure with its label, its line and the hairline of its tone', () => {
    const fixture = render(Kpi, { label: 'Vues', value: '1 234', sub: '+5 %', tone: 'bad' });

    expect(all(fixture, 'span').map(words)).toEqual(['', 'Vues', '1 234', '+5 %']);
    expect(root(fixture).querySelector('[aria-hidden="true"]')?.className).toContain('bg-danger');
  });

  it('dresses a badge in its tone, and a blank tone as a neutral one', () => {
    const fixture = TestBed.createComponent(BadgeHost);
    fixture.detectChanges();
    expect(root(fixture).querySelector('span')?.className).toContain('text-danger-light');

    fixture.componentInstance.tone.set('');
    fixture.detectChanges();
    expect(root(fixture).querySelector('span')?.className).toBe('hx-chip');
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
    expect(words(toggle)).toBe('admin.rank.less');
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

  it('pages a journal read by cursor while the API says more follows', () => {
    const fixture = render(AdminPager, { page: 1, hasMore: true });

    expect(all(fixture, 'a').map((link) => link.getAttribute('href'))).toEqual(['/?page=2']);
    expect(words(root(fixture).querySelector('nav span'))).toBe('admin.pager.page');

    fixture.componentRef.setInput('hasMore', false);
    fixture.detectChanges();
    expect(all(fixture, 'nav')).toHaveLength(0);
  });

  it('asks for a confirmation before it runs an action', async () => {
    const fixture = render(ConfirmButton, { label: 'Supprimer', confirmLabel: 'Confirmer' });
    const confirmed = vi.fn();
    fixture.componentInstance.confirmed.subscribe(confirmed);

    root(fixture).querySelector('button')?.click();
    fixture.detectChanges();
    await fixture.whenStable();
    const [confirm, cancel] = all(fixture, 'button');
    expect(words(confirm)).toBe('Confirmer');
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

  it('marks the current period and links the others with the first page', () => {
    const fixture = render(RangeBar, { current: '90d' });

    const links = all(fixture, 'a');
    expect(links.map((link) => link.getAttribute('href'))).toEqual([
      '/?range=7d',
      '/?range=30d',
      '/?range=90d',
      '/?range=all',
    ]);
    expect(links.filter((link) => link.getAttribute('aria-current') === 'page')).toEqual([
      links[2],
    ]);
  });

  it('titles the document as a private admin page, once the title is known', () => {
    const fixture = render(PageHead, { eyebrow: 'Gestion', title: '' });
    TestBed.tick();
    expect(apply).not.toHaveBeenCalled();

    fixture.componentRef.setInput('title', 'Comptes');
    fixture.componentRef.setInput('subtitle', 'Tous les comptes');
    fixture.detectChanges();
    TestBed.tick();

    expect(root(fixture).querySelector('h1')?.textContent?.trim()).toBe('Comptes');
    expect(words(root(fixture))).toContain('Tous les comptes');
    expect(apply).toHaveBeenCalledWith({
      kind: 'private',
      titleFormat: 'admin',
      locale: 'fr',
      title: 'Comptes',
    });
  });

  it('names each series of a legend, with the value of a slice when it has one', () => {
    const fixture = render(Legend, {
      items: [
        { label: 'Vues', color: 'var(--color-gold)' },
        { label: 'Mobile', color: 'var(--color-hex)', value: '40.0 %' },
      ],
    });

    expect(all(fixture, 'li').map(words)).toEqual(['Vues', 'Mobile 40.0 %']);
    expect(all(fixture, 'li > span')[1]?.style.background).toBe('var(--color-hex)');
  });

  it('heads a card with its title and what it counts', () => {
    const fixture = render(AdminCard, { heading: 'Pages', chip: 'vues' });

    expect(words(root(fixture).querySelector('h2'))).toBe('Pages');
    expect(words(root(fixture).querySelector('[lodbChip]'))).toBe('vues');
    expect(all(render(AdminCard, {}), 'h2')).toHaveLength(0);
  });
});
