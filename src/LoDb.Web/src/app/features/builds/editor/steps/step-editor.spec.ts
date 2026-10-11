import { type ComponentFixture, TestBed } from '@angular/core/testing';
import { provideTransloco } from '@jsverse/transloco';
import { of } from 'rxjs';
import { EditorCatalogs } from '../catalog/editor-catalogs';
import { EDITOR_ENTRY } from '../form/editor-entry-token';
import { ArmoryOpener } from '../items/armory-opener';
import { editorEntryOf } from '../testing/editor-entry-of';
import { EditorAnnouncer } from './editor-announcer';
import { STEP_LIMITS } from './order/step-limits';
import { StepEditing } from './step-editing';
import { StepEditor } from './step-editor';

const LABELS = { build: { editor: { counter: '{{count}} / {{max}}' } } };

async function mount(): Promise<{ fixture: ComponentFixture<StepEditor>; open: () => void }> {
  const open = vi.fn();
  TestBed.configureTestingModule({
    providers: [
      StepEditing,
      {
        provide: EDITOR_ENTRY,
        useValue: editorEntryOf({
          steps: [
            { label: 'Start', items: [] },
            { label: 'Core', items: [] },
          ],
        }),
      },
      { provide: EditorAnnouncer, useValue: { say: vi.fn() } },
      { provide: EditorCatalogs, useValue: { resolveItem: () => undefined, ghostOf: () => null } },
      provideTransloco({
        config: {
          availableLangs: ['en'],
          defaultLang: 'en',
          missingHandler: { logMissingKey: false },
          prodMode: true,
        },
        loader: class {
          getTranslation = () => of(LABELS);
        },
      }),
    ],
  });
  // The editor provides its own opener, which a later provider replaces; the dialog it opens
  // is ArmoryOpener's spec.
  TestBed.overrideComponent(StepEditor, {
    add: { providers: [{ provide: ArmoryOpener, useValue: { open } }] },
  });
  const fixture = TestBed.createComponent(StepEditor);
  await fixture.whenStable();
  return { fixture, open };
}

function editorOf(fixture: ComponentFixture<StepEditor>): HTMLElement {
  return fixture.nativeElement as HTMLElement;
}

describe('StepEditor', () => {
  it('opens the armory of the step whose Add item tile is pressed', async () => {
    const { fixture, open } = await mount();

    editorOf(fixture).querySelectorAll<HTMLButtonElement>('.forge-additem')[1]?.click();

    expect(open).toHaveBeenCalledExactlyOnceWith(1);
  });

  // An Arabic page lays its lines out right to left: unisolated, "0 / 8" would read "8 / 0".
  it('isolates its counters left to right, whatever the direction of the page', async () => {
    const { fixture } = await mount();
    const counters = [...editorOf(fixture).querySelectorAll('.forge-step__count')];

    expect(counters).toHaveLength(2);
    for (const counter of counters) {
      const fraction = counter.querySelector('bdi');
      expect(fraction?.getAttribute('dir')).toBe('ltr');
      expect(fraction?.textContent?.trim()).toBe(`0 / ${STEP_LIMITS.maxItemsPerStep}`);
    }
  });
});
