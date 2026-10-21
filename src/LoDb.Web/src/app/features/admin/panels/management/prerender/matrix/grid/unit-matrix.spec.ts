import { type ComponentFixture, TestBed } from '@angular/core/testing';
import { configureAdminTestBed } from '../../../../../testing/admin-test-bed';
import { type MatrixVersion, UnitMatrix } from './unit-matrix';

const VIEW_KEY = 'lodb.admin.prerender.matrix-view';
const LOCALES = ['de', 'fr', 'zh-hans'];
const VERSIONS: MatrixVersion[] = [
  { version: '16.18.1', cells: 'crz', rank: 0 },
  { version: '15.2.1', cells: 'cf.', rank: 3 },
];

function render(inputs: Readonly<Record<string, unknown>> = {}): ComponentFixture<UnitMatrix> {
  const fixture = TestBed.createComponent(UnitMatrix);
  const all = { mode: 'pass', locales: LOCALES, versions: VERSIONS, ...inputs };
  for (const [name, value] of Object.entries(all)) {
    fixture.componentRef.setInput(name, value);
  }
  fixture.detectChanges();
  return fixture;
}

function page(fixture: ComponentFixture<UnitMatrix>): HTMLElement {
  return fixture.nativeElement as HTMLElement;
}

function button(fixture: ComponentFixture<UnitMatrix>, label: string): HTMLButtonElement {
  const found = [...page(fixture).querySelectorAll('button')].find(
    (candidate) => candidate.textContent?.trim() === label,
  );
  if (!found) {
    throw new Error(`No button reads "${label}".`);
  }
  return found;
}

describe('UnitMatrix', () => {
  beforeEach(() => {
    localStorage.removeItem(VIEW_KEY);
    configureAdminTestBed();
  });
  // Spec files of a worker share one page: a view left in storage would reach the next file.
  afterEach(() => localStorage.removeItem(VIEW_KEY));

  it('heads a column per locale, short codes for Chinese', () => {
    const heads = [...page(render()).querySelectorAll('.matrix-head')].map((head) =>
      head.textContent?.trim(),
    );

    expect(heads).toEqual(['de', 'fr', 'zhs']);
  });

  it('groups the versions by season, each folded or unfolded by its button', () => {
    const fixture = render();
    const seasons = () => [...page(fixture).querySelectorAll('.matrix-season')];
    expect(seasons().map((season) => season.getAttribute('aria-expanded'))).toEqual([
      'true',
      'true',
    ]);
    expect(page(fixture).querySelectorAll('[data-v="15.2.1"]')).toHaveLength(3);

    (seasons()[1] as HTMLButtonElement).click();
    fixture.detectChanges();
    expect(seasons()[1]?.getAttribute('aria-expanded')).toBe('false');
    expect(page(fixture).querySelectorAll('[data-v="15.2.1"]')).toHaveLength(0);
    expect(page(fixture).querySelectorAll('.cell--season')).toHaveLength(3);
  });

  it('fills a running cell and a stopped one as far as their unit went', () => {
    const fixture = render({
      running: [
        { version: '16.18.1', locale: 'fr', share: 0.27 },
        { version: '16.18.1', locale: 'zh-hans', share: 0.5 },
      ],
    });
    const cell = (locale: string) =>
      page(fixture).querySelector<HTMLElement>(`[data-v="16.18.1"][data-l="${locale}"]`);

    expect(cell('fr')?.dataset['c']).toBe('r');
    expect(cell('fr')?.style.getPropertyValue('--fill')).toBe('27');
    expect(cell('zh-hans')?.dataset['c']).toBe('z');
    expect(cell('zh-hans')?.style.getPropertyValue('--fill')).toBe('50');
    expect(cell('de')?.style.getPropertyValue('--fill')).toBe('');
  });

  it('names in its legend the states its cells show, and those only', () => {
    const legend = (fixture: ComponentFixture<UnitMatrix>) =>
      [...page(fixture).querySelectorAll('.matrix-legend .cell')].map((cell) =>
        cell.getAttribute('data-c'),
      );

    expect(legend(render())).toEqual(['c', 'r', 'z', '.', 'f']);
    // A cancelled pass: no unit runs, one was cut short.
    expect(legend(render({ versions: [{ version: '16.20.1', cells: 'ci.' }] }))).toEqual([
      'c',
      '.',
      'i',
    ]);
  });

  it('opens the detail of a version from a cell or from its head', () => {
    const fixture = render();
    const opened: string[] = [];
    fixture.componentInstance.opened.subscribe((version) => opened.push(version));

    page(fixture).querySelector<HTMLElement>('[data-v="15.2.1"][data-l="fr"]')?.click();
    button(fixture, '16.18.1').click();

    expect(opened).toEqual(['15.2.1', '16.18.1']);
  });

  it('toggles the selection of a version in a coverage', () => {
    const fixture = render({ mode: 'coverage', versions: [{ version: '16.1.1', cells: 'cp-' }] });
    const toggled: string[] = [];
    fixture.componentInstance.toggled.subscribe((version) => toggled.push(version));
    fixture.componentRef.setInput('selected', new Set(['16.1.1']));
    fixture.detectChanges();

    const head = button(fixture, '16.1.1');
    expect(head.getAttribute('aria-pressed')).toBe('true');
    head.click();
    expect(toggled).toEqual(['16.1.1']);
  });

  it('turns into a table on demand, and remembers it in this browser', () => {
    const fixture = render();

    button(fixture, 'admin.prerender.common.matrix.view_table').click();
    fixture.detectChanges();

    expect(page(fixture).querySelector('lodb-matrix-table table')).not.toBeNull();
    expect(page(fixture).querySelector('.matrix-frame')).toBeNull();
    expect(localStorage.getItem(VIEW_KEY)).toBe('table');
    expect(page(render()).querySelector('lodb-matrix-table')).not.toBeNull();
  });
});
