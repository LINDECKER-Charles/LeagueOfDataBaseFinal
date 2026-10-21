import { type ComponentFixture, TestBed } from '@angular/core/testing';
import { prerenderProgress } from '../../../../../testing/fixtures/prerender-progress';
import { configureAdminTestBed } from '../../../../../testing/admin-test-bed';
import { matrixRows } from '../grid/matrix-rows';
import type { MatrixVersion } from '../grid/unit-matrix';
import { MatrixTable } from './matrix-table';

const PROGRESS = prerenderProgress().versions[0];
const VERSIONS: MatrixVersion[] = [PROGRESS, { version: '15.2.1', cells: 'ccf', rank: 3 }];

function render(mode: 'pass' | 'coverage'): ComponentFixture<MatrixTable> {
  const fixture = TestBed.createComponent(MatrixTable);
  fixture.componentRef.setInput('seasons', matrixRows(VERSIONS, { mode, folds: new Map() }));
  fixture.componentRef.setInput('mode', mode);
  fixture.detectChanges();
  return fixture;
}

function texts(root: ParentNode, selector: string): string[] {
  return [...root.querySelectorAll(selector)].map((node) =>
    (node.textContent ?? '').replace(/\s+/g, ' ').trim(),
  );
}

describe('MatrixTable', () => {
  beforeEach(() => configureAdminTestBed());

  it('heads a tbody per season with a rowgroup header, a row per version', () => {
    const table = render('pass').nativeElement as HTMLElement;

    expect(table.querySelectorAll('tbody')).toHaveLength(2);
    expect(table.querySelectorAll('th[scope="rowgroup"]')).toHaveLength(2);
    expect(texts(table, 'tbody th[scope="row"]')).toEqual(['16.18.1', '15.2.1']);
  });

  it('counts the locales of a version and gives its pages and volume when it has them', () => {
    const table = render('pass').nativeElement as HTMLElement;
    const [first, second] = [...table.querySelectorAll('tbody tr:has(th[scope="row"])')];

    expect(texts(first, 'td')).toEqual([
      'admin.prerender.common.cells.running',
      '6 / 21',
      '1',
      '1',
      '7 140',
      '278.46 MB',
      'admin.prerender.common.matrix.detail',
    ]);
    expect(texts(second, 'td').slice(5, 6)).toEqual(['—']);
  });

  it('opens the detail of a version from its button, in a pass only', () => {
    const fixture = render('pass');
    const opened: string[] = [];
    fixture.componentInstance.opened.subscribe((version) => opened.push(version));

    (fixture.nativeElement as HTMLElement)
      .querySelector('tbody button')
      ?.dispatchEvent(new MouseEvent('click'));
    expect(opened).toEqual(['16.18.1']);
    expect((render('coverage').nativeElement as HTMLElement).querySelector('button')).toBeNull();
  });
});
