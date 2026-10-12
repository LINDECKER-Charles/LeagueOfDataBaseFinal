import { Component, signal } from '@angular/core';
import { type ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { provideTestTransloco } from '../../../../core/testing/provide-test-transloco';
import { ChampionPicker } from '../champion/champion-picker';
import { EditorContext } from '../context/editor-context';
import { RuneBoard } from '../runes/rune-board';
import { BuildSaver } from '../save/build-saver';
import { StepEditor } from '../steps/step-editor';
import { editorEntryOf } from '../testing/editor-entry-of';
import { BuildEditor } from './build-editor';
import { BuildEditorStore } from './build-editor-store';
import { EDITOR_ENTRY } from './editor-entry-token';

// The sections around the identity, each read from the API and specified on its own.
@Component({ selector: 'lodb-editor-context', template: '' })
class ContextSection {}

@Component({ selector: 'lodb-champion-picker', template: '' })
class ChampionSection {}

@Component({ selector: 'lodb-rune-board', template: '' })
class RuneSection {}

@Component({ selector: 'lodb-step-editor', template: '' })
class StepSection {}

const VALUE = Object.getOwnPropertyDescriptor(HTMLInputElement.prototype, 'value');
// The bounds of a build name (LoDb.Domain BuildLimits), which the API checks again.
const NAME_MIN_LENGTH = 3;
const NAME_MAX_LENGTH = 80;

interface Mounted {
  readonly fixture: ComponentFixture<BuildEditor>;
  readonly name: HTMLInputElement;
  readonly save: () => Promise<void>;
}

async function mount(): Promise<Mounted> {
  const save = vi.fn(() => Promise.resolve());
  TestBed.configureTestingModule({
    providers: [provideRouter([]), provideTestTransloco()],
  });
  // The editor provides its entry and its saver itself: the later providers take their place.
  TestBed.overrideComponent(BuildEditor, {
    remove: { imports: [ChampionPicker, EditorContext, RuneBoard, StepEditor] },
    add: {
      imports: [ChampionSection, ContextSection, RuneSection, StepSection],
      providers: [
        { provide: EDITOR_ENTRY, useValue: editorEntryOf() },
        { provide: BuildSaver, useValue: { saving: signal(false), save } },
      ],
    },
  });
  const fixture = TestBed.createComponent(BuildEditor);
  await fixture.whenStable();
  const name = (fixture.nativeElement as HTMLElement).querySelector('input[name="name"]');
  if (!(name instanceof HTMLInputElement)) {
    throw new Error('the editor has no name field');
  }
  return { fixture, name, save };
}

// What a keystroke does: the browser writes the value, then tells the page.
async function type(fixture: ComponentFixture<BuildEditor>, field: HTMLInputElement, text: string) {
  VALUE?.set?.call(field, text);
  field.dispatchEvent(new Event('input'));
  await fixture.whenStable();
}

async function submit(fixture: ComponentFixture<BuildEditor>): Promise<void> {
  const editor = fixture.nativeElement as HTMLElement;
  editor.querySelector<HTMLButtonElement>('button[type="submit"]')?.click();
  await fixture.whenStable();
}

describe('BuildEditor', () => {
  it('asks the browser for a name of 3 to 80 characters', async () => {
    const { name } = await mount();

    expect(name.required).toBe(true);
    expect(name.minLength).toBe(NAME_MIN_LENGTH);
    expect(name.maxLength).toBe(NAME_MAX_LENGTH);
  });

  // The browser only judges the length of what was typed: a value the page writes back, even
  // the same one, would let a short name through.
  it('never writes the name back while it is typed', async () => {
    const { fixture, name } = await mount();
    const written = vi.fn();
    Object.defineProperty(name, 'value', {
      configurable: true,
      get: () => VALUE?.get?.call(name),
      set: (value: string) => {
        written(value);
        VALUE?.set?.call(name, value);
      },
    });

    await type(fixture, name, 'ab');

    expect(fixture.debugElement.injector.get(BuildEditorStore).name()).toBe('ab');
    expect(written).not.toHaveBeenCalled();
  });

  // jsdom never finds a value too short, which only a typing user makes: an empty name stands
  // for the names the browser refuses.
  it('sends nothing while the browser refuses the name, then saves once it passes', async () => {
    const { fixture, name, save } = await mount();

    await submit(fixture);
    expect(save).not.toHaveBeenCalled();

    await type(fixture, name, 'Burst mid');
    await submit(fixture);
    expect(save).toHaveBeenCalledOnce();
  });
});
