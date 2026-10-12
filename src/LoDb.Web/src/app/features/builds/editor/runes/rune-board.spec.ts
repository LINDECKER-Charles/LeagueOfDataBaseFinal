import { signal } from '@angular/core';
import { type ComponentFixture, TestBed } from '@angular/core/testing';
import type { RuneTreeOption } from '../../../../core/api/generated/models/rune-tree-option';
import { provideTestTransloco } from '../../../../core/testing/provide-test-transloco';
import { EditorCatalogs } from '../catalog/editor-catalogs';
import { EDITOR_ENTRY } from '../form/editor-entry-token';
import { editorEntryOf } from '../testing/editor-entry-of';
import { RuneBoard } from './rune-board';
import { RuneEditing } from './rune-editing';

const LABELS = {
  build: { editor: { runes: { primary: 'Primary path', secondary: 'Secondary path' } } },
};

// A tree whose rows hold the given rune ids, the keystones first; a rune is named by its id.
function tree(id: number, name: string, rows: number[][]): RuneTreeOption {
  const image = { status: 'absent' as const };
  const slots = rows.map((row) =>
    row.map((runeId) => ({
      id: runeId,
      key: `${runeId}`,
      name: `${runeId}`,
      image,
      shortDesc: '',
    })),
  );
  return { id, key: name, name, image, slots };
}

const TREES = [
  tree(8000, 'Precision', [
    [8005, 8008],
    [9101, 9111],
    [9104, 9105],
    [8014, 8017],
  ]),
  tree(8100, 'Domination', [
    [8112, 8124],
    [8126, 8139],
    [8138, 8135],
    [8106, 8105],
  ]),
];

function mount(): ComponentFixture<RuneBoard> {
  TestBed.configureTestingModule({
    providers: [
      RuneEditing,
      { provide: EDITOR_ENTRY, useValue: editorEntryOf() },
      {
        provide: EditorCatalogs,
        useValue: { runes: { options: signal(TREES), status: signal('ready'), retry: vi.fn() } },
      },
      provideTestTransloco({ otherwise: LABELS }),
    ],
  });
  return TestBed.createComponent(RuneBoard);
}

async function press(fixture: ComponentFixture<RuneBoard>, button: Element | undefined) {
  (button as HTMLButtonElement | undefined)?.click();
  await fixture.whenStable();
}

function boardOf(fixture: ComponentFixture<RuneBoard>): HTMLElement {
  return fixture.nativeElement as HTMLElement;
}

function treeButton(fixture: ComponentFixture<RuneBoard>, side: string, name: string) {
  const group = boardOf(fixture).querySelector(`[aria-label="${side}"]`);
  return [...(group?.querySelectorAll('button') ?? [])].find((button) =>
    button.textContent?.includes(name),
  );
}

function rune(fixture: ComponentFixture<RuneBoard>, id: number) {
  return boardOf(fixture).querySelector(`.rune-perk[aria-label="${id}"]`) ?? undefined;
}

function pressedRunes(fixture: ComponentFixture<RuneBoard>): (string | null)[] {
  return [...boardOf(fixture).querySelectorAll('.rune-perk[aria-pressed="true"]')].map((perk) =>
    perk.getAttribute('aria-label'),
  );
}

describe('RuneBoard', () => {
  it('drops the oldest secondary rune for one in a third row', async () => {
    const fixture = mount();
    await fixture.whenStable();
    await press(fixture, treeButton(fixture, 'Primary path', 'Precision'));
    for (const id of [8005, 9101, 9104, 8014]) {
      await press(fixture, rune(fixture, id));
    }
    await press(fixture, treeButton(fixture, 'Secondary path', 'Domination'));
    await press(fixture, rune(fixture, 8126));
    await press(fixture, rune(fixture, 8138));
    expect(pressedRunes(fixture)).toEqual(['8005', '9101', '9104', '8014', '8126', '8138']);

    await press(fixture, rune(fixture, 8106));

    expect(pressedRunes(fixture)).toEqual(['8005', '9101', '9104', '8014', '8138', '8106']);
  });
});
