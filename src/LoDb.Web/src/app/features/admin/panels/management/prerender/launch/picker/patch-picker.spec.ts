import { ChangeDetectionStrategy, Component, signal } from '@angular/core';
import { type ComponentFixture, TestBed } from '@angular/core/testing';
import type { PrerenderPatchRow } from '../../../../../../../core/api/generated/models/prerender-patch-row';
import { configureAdminTestBed } from '../../../../../testing/admin-test-bed';
import { prerenderPatches } from '../../../../../testing/fixtures/prerender-patches';
import {
  type PatchSelection,
  type SelectionAction,
  selectionAfter,
} from '../../common/selection/patch-selection';
import { SelectionUndo } from '../../common/selection/selection-undo';
import { PatchPicker } from './patch-picker';

const ROW = prerenderPatches().patches[0];
// Two seasons: 16 open by default (the newest), 15 folded and absent from the epoch.
const VERSIONS = '16.12.1 16.11.1 16.10.1 16.9.1 16.8.1 16.7.1 15.24.1 15.23.1'.split(' ');
const ABSENT = '-'.repeat(21);
const ROWS: PrerenderPatchRow[] = VERSIONS.map((version) => ({
  ...ROW,
  version,
  cells: version.startsWith('15') ? ABSENT : ROW.cells,
}));
const COVERAGE = prerenderPatches({ patches: ROWS });

@Component({
  imports: [PatchPicker],
  template: `
    <lodb-patch-picker
      [patches]="coverage"
      [selection]="selection()"
      [canUndo]="undo().size > 0"
      (acted)="act($event)"
      (undone)="back()"
    />
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
class Host {
  readonly coverage = COVERAGE;
  readonly selection = signal<PatchSelection>({
    selected: new Set(),
    anchor: null,
    priority: [],
    extent: null,
  });
  readonly undo = signal(SelectionUndo.empty());

  act(action: SelectionAction): void {
    this.undo.update((undo) => undo.after(this.selection(), action));
    this.selection.update((selection) => selectionAfter(selection, action));
  }

  back(): void {
    const step = this.undo().undo();
    if (step) {
      this.undo.set(step.rest);
      this.selection.set(step.selection);
    }
  }
}

type Fixture = ComponentFixture<Host>;

async function open(): Promise<Fixture> {
  configureAdminTestBed();
  const fixture = TestBed.createComponent(Host);
  fixture.detectChanges();
  await fixture.whenStable();
  return fixture;
}

function page(fixture: Fixture): HTMLElement {
  return fixture.nativeElement as HTMLElement;
}

function row(fixture: Fixture, key: string): HTMLElement {
  const found = page(fixture).querySelector<HTMLElement>(`[data-key="${key}"]`);
  if (!found) {
    throw new Error(`No row ${key}.`);
  }
  return found;
}

function ticked(fixture: Fixture): string[] {
  return ROWS.map((patch) => patch.version).filter((version) =>
    fixture.componentInstance.selection().selected.has(version),
  );
}

async function settle(fixture: Fixture): Promise<void> {
  fixture.detectChanges();
  await fixture.whenStable();
}

async function key(fixture: Fixture, on: string, pressed: string | KeyboardEventInit) {
  const init = typeof pressed === 'string' ? { key: pressed } : pressed;
  row(fixture, on).dispatchEvent(new KeyboardEvent('keydown', { ...init, bubbles: true }));
  await settle(fixture);
}

async function click(fixture: Fixture, on: string, shiftKey = false) {
  row(fixture, on).dispatchEvent(new MouseEvent('click', { shiftKey, bubbles: true }));
  await settle(fixture);
}

describe('PatchPicker', () => {
  it('shows the newest season open, the others folded, as a treegrid', async () => {
    const fixture = await open();

    const grid = page(fixture).querySelector('[role="treegrid"]');
    expect(grid?.getAttribute('aria-multiselectable')).toBe('true');
    expect(row(fixture, 'season:16').getAttribute('aria-expanded')).toBe('true');
    expect(row(fixture, 'season:15').getAttribute('aria-expanded')).toBe('false');
    expect(page(fixture).querySelector('[data-key="15.24.1"]')).toBeNull();
    expect(page(fixture).querySelectorAll('[tabindex="0"]')).toHaveLength(1);
  });

  it('names its columns in a head row, outside the rows a key moves through', async () => {
    const fixture = await open();
    const heads = [...page(fixture).querySelectorAll('[data-picker-head] [role="columnheader"]')];

    expect(heads.map((head) => head.textContent?.trim())).toEqual([
      'admin.prerender.launcher.patches.columns.select',
      'admin.prerender.launcher.patches.columns.version',
      'admin.prerender.launcher.patches.columns.coverage',
      'admin.prerender.launcher.patches.columns.pages',
      'admin.prerender.launcher.patches.columns.status',
      'admin.prerender.launcher.patches.columns.priority',
    ]);
    expect(page(fixture).querySelector('[data-picker-head]')?.hasAttribute('tabindex')).toBe(false);
  });

  it('ticks a row on a click, a range on a Shift click', async () => {
    const fixture = await open();

    await click(fixture, '16.11.1');
    expect(row(fixture, '16.11.1').getAttribute('aria-selected')).toBe('true');

    await click(fixture, '16.8.1', true);
    expect(ticked(fixture)).toEqual(['16.11.1', '16.10.1', '16.9.1', '16.8.1']);
  });

  it('ticks a season with its box: all, then none; mixed in between', async () => {
    const fixture = await open();
    const box = () => row(fixture, 'season:16').querySelector<HTMLInputElement>('input')!;

    await click(fixture, '16.12.1');
    expect(box().indeterminate).toBe(true);

    box().click();
    await settle(fixture);
    expect(ticked(fixture)).toEqual([
      '16.12.1',
      '16.11.1',
      '16.10.1',
      '16.9.1',
      '16.8.1',
      '16.7.1',
    ]);
    expect(box().checked).toBe(true);
    expect(row(fixture, 'season:16').getAttribute('aria-expanded')).toBe('true');

    box().click();
    await settle(fixture);
    expect(ticked(fixture)).toEqual([]);
  });

  it('renders a patch first with P and its star', async () => {
    const fixture = await open();

    await key(fixture, '16.10.1', 'p');
    const star = row(fixture, '16.10.1').querySelector('button');
    expect(star?.textContent?.trim()).toBe('★ 1');
    expect(star?.getAttribute('aria-pressed')).toBe('true');
    expect(ticked(fixture)).toEqual(['16.10.1']);

    star?.click();
    await settle(fixture);
    expect(fixture.componentInstance.selection().priority).toEqual([]);
  });

  it('moves with the arrows and draws a range with Shift, which shrinks back', async () => {
    const fixture = await open();

    await key(fixture, '16.12.1', ' ');
    for (const from of ['16.12.1', '16.11.1', '16.10.1', '16.9.1']) {
      await key(fixture, from, { key: 'ArrowDown', shiftKey: true });
    }
    expect(ticked(fixture)).toEqual(['16.12.1', '16.11.1', '16.10.1', '16.9.1', '16.8.1']);
    expect(document.activeElement).toBe(row(fixture, '16.8.1'));

    for (const from of ['16.8.1', '16.9.1', '16.10.1']) {
      await key(fixture, from, { key: 'ArrowUp', shiftKey: true });
    }
    expect(ticked(fixture)).toEqual(['16.12.1', '16.11.1']);
    expect(row(fixture, '16.11.1').getAttribute('tabindex')).toBe('0');
  });

  it('unfolds a season with the arrow, and undoes a grouped selection', async () => {
    const fixture = await open();

    await key(fixture, 'season:15', 'ArrowRight');
    expect(row(fixture, '15.24.1')).toBeTruthy();

    row(fixture, '15.24.1').dispatchEvent(
      new KeyboardEvent('keydown', { key: 'a', ctrlKey: true, bubbles: true }),
    );
    await settle(fixture);
    expect(ticked(fixture)).toHaveLength(8);

    row(fixture, '15.24.1').dispatchEvent(
      new KeyboardEvent('keydown', { key: 'z', ctrlKey: true, bubbles: true }),
    );
    await settle(fixture);
    expect(ticked(fixture)).toEqual([]);
  });

  it('goes to the filter with /, which Escape clears before the dialog closes', async () => {
    const fixture = await open();
    const filter = page(fixture).querySelector<HTMLInputElement>('[data-filter]')!;

    await key(fixture, '16.12.1', '/');
    expect(document.activeElement).toBe(filter);

    filter.value = '16.1';
    filter.dispatchEvent(new Event('input'));
    await settle(fixture);
    expect(page(fixture).querySelector('[data-key="16.11.1"]')).toBeNull();
    expect(page(fixture).querySelector('[data-key="16.12.1"]')).toBeNull();

    const escape = new KeyboardEvent('keydown', { key: 'Escape', bubbles: true, cancelable: true });
    filter.dispatchEvent(escape);
    await settle(fixture);
    expect(escape.defaultPrevented).toBe(true);
    expect(page(fixture).querySelector('[data-key="16.11.1"]')).not.toBeNull();

    const again = new KeyboardEvent('keydown', { key: 'Escape', bubbles: true, cancelable: true });
    filter.dispatchEvent(again);
    expect(again.defaultPrevented).toBe(false);
  });

  it('says when no patch matches, and clears the filter', async () => {
    const fixture = await open();
    const filter = page(fixture).querySelector<HTMLInputElement>('[data-filter]')!;

    filter.value = '3.15';
    filter.dispatchEvent(new Event('input'));
    await settle(fixture);
    const empty = page(fixture).querySelector('[data-no-match]');
    expect(empty?.textContent).toContain('admin.prerender.launcher.patches.no_match');

    empty?.querySelector('button')?.click();
    await settle(fixture);
    expect(page(fixture).querySelector('[role="treegrid"]')).not.toBeNull();
  });

  it('searches the versions typed', async () => {
    const fixture = await open();

    await key(fixture, 'season:16', '1');
    await key(fixture, '16.12.1', '6');
    await key(fixture, '16.12.1', '.');
    await key(fixture, '16.12.1', '9');

    expect(document.activeElement).toBe(row(fixture, '16.9.1'));
  });

  it('counts the selection, the hidden part, and announces it once settled', async () => {
    const fixture = await open();

    await click(fixture, '16.12.1');
    await key(fixture, '16.12.1', 'p');
    await key(fixture, 'season:15', ' ');
    expect(page(fixture).querySelector('[data-count]')?.textContent?.trim()).toBe(
      'admin.prerender.launcher.patches.count · admin.prerender.launcher.patches.count_priority',
    );
    expect(page(fixture).querySelector('[data-announce]')?.textContent?.trim()).toBe('');

    await new Promise((resolve) => setTimeout(resolve, 550));
    await settle(fixture);
    expect(page(fixture).querySelector('[data-announce]')?.textContent?.trim()).toBe(
      'admin.prerender.launcher.patches.announce',
    );
  });

  it('selects a range by its bounds, whatever the filters', async () => {
    const fixture = await open();
    const from = page(fixture).querySelector<HTMLSelectElement>('[data-range-from]')!;
    const to = page(fixture).querySelector<HTMLSelectElement>('[data-range-to]')!;
    from.value = '16.8.1';
    from.dispatchEvent(new Event('change'));
    to.value = '15.24.1';
    to.dispatchEvent(new Event('change'));

    const apply = [...page(fixture).querySelectorAll('button')].find(
      (button) => button.textContent?.trim() === 'admin.prerender.launcher.patches.range_apply',
    );
    apply?.click();
    await settle(fixture);

    expect(ticked(fixture)).toEqual(['16.8.1', '16.7.1', '15.24.1']);
  });

  it('replaces the selection with the visible patches not covered', async () => {
    const fixture = await open();
    await click(fixture, '16.12.1');

    page(fixture).querySelector<HTMLButtonElement>('[data-tool="uncovered"]')?.click();
    await settle(fixture);

    expect(ticked(fixture)).toEqual(['15.24.1', '15.23.1']);
  });
});
