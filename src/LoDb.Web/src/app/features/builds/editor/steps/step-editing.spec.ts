import { TestBed } from '@angular/core/testing';
import type { BuildStep } from '../../../../core/api/generated/models/build-step';
import { EDITOR_ENTRY } from '../form/editor-entry-token';
import { editorEntryOf } from '../testing/editor-entry-of';
import { EditorAnnouncer } from './editor-announcer';
import { STEP_LIMITS } from './order/step-limits';
import { StepEditing } from './step-editing';

function setUp(steps: BuildStep[] = []) {
  const say = vi.fn<(key: string, params?: object) => void>();
  TestBed.configureTestingModule({
    providers: [
      StepEditing,
      { provide: EDITOR_ENTRY, useValue: editorEntryOf({ steps }) },
      { provide: EditorAnnouncer, useValue: { say } },
    ],
  });
  return { editing: TestBed.inject(StepEditing), say };
}

function step(label: string, items: string[]): BuildStep {
  return { label, items };
}

function itemsOf(editing: StepEditing, index = 0): readonly string[] {
  return editing.order().steps[index]?.items ?? [];
}

describe('StepEditing', () => {
  it('opens a build without steps on one blank step', () => {
    const { editing } = setUp();

    expect(editing.order().steps).toEqual([{ label: '', note: null, items: [] }]);
  });

  it('opens the steps of the build, notes included', () => {
    const { editing } = setUp([{ label: 'Start', note: 'early', items: ['1055'] }]);

    expect(editing.order().steps).toEqual([{ label: 'Start', note: 'early', items: ['1055'] }]);
  });

  it('adds, edits and removes steps silently', () => {
    const { editing, say } = setUp([step('Start', ['1055'])]);

    editing.appendStep('Core');
    editing.editStep(1, { note: 'rush it' });
    expect(editing.order().steps[1]).toEqual({ label: 'Core', note: 'rush it', items: [] });

    editing.deleteStep(0);
    expect(editing.order().steps.map((kept) => kept.label)).toEqual(['Core']);
    expect(say).not.toHaveBeenCalled();
  });

  it('announces a step moved by its buttons, at its new position', () => {
    const { editing, say } = setUp([step('A', []), step('B', [])]);

    editing.shiftStep(0, 1);

    expect(editing.order().steps.map((moved) => moved.label)).toEqual(['B', 'A']);
    expect(say).toHaveBeenCalledExactlyOnceWith('build.editor.dnd.moved_step', { position: 2 });
  });

  it('stays silent on a move that changes nothing', () => {
    const { editing, say } = setUp([step('A', ['a']), step('B', [])]);

    editing.shiftStep(0, -1);
    editing.dropStep(1, 2);
    editing.shiftItem({ step: 0, index: 0 }, 1);

    expect(say).not.toHaveBeenCalled();
  });

  it('announces a dropped step where it rests', () => {
    const { editing, say } = setUp([step('A', []), step('B', []), step('C', [])]);

    editing.dropStep(0, 3);

    expect(editing.order().steps.map((moved) => moved.label)).toEqual(['B', 'C', 'A']);
    expect(say).toHaveBeenCalledExactlyOnceWith('build.editor.dnd.moved_step', { position: 3 });
  });

  it('announces an item added to a step, never beyond its cap', () => {
    const full = Array.from({ length: STEP_LIMITS.maxItemsPerStep }, () => '2003');
    const { editing, say } = setUp([step('A', []), step('B', full)]);

    editing.appendItem(0, '1055');
    editing.appendItem(1, '1055');

    expect(itemsOf(editing, 0)).toEqual(['1055']);
    expect(itemsOf(editing, 1)).toHaveLength(STEP_LIMITS.maxItemsPerStep);
    expect(say).toHaveBeenCalledExactlyOnceWith('build.editor.dnd.added', { step: 1 });
  });

  it('announces an item moved by its buttons, and removes one silently', () => {
    const { editing, say } = setUp([step('A', ['a', 'b', 'c'])]);

    editing.shiftItem({ step: 0, index: 0 }, 1);
    expect(itemsOf(editing)).toEqual(['b', 'a', 'c']);
    expect(say).toHaveBeenCalledExactlyOnceWith('build.editor.dnd.moved_item', { position: 2 });

    editing.deleteItem({ step: 0, index: 2 });
    expect(itemsOf(editing)).toEqual(['b', 'a']);
    expect(say).toHaveBeenCalledOnce();
  });

  it('announces an item dropped within its step where it rests', () => {
    const { editing, say } = setUp([step('A', ['a', 'b', 'c'])]);

    editing.dropItem({ step: 0, index: 0 }, { step: 0, index: 3 });

    expect(itemsOf(editing)).toEqual(['b', 'c', 'a']);
    expect(say).toHaveBeenCalledExactlyOnceWith('build.editor.dnd.moved_item', { position: 3 });
  });

  it('announces an item dropped into another step', () => {
    const { editing, say } = setUp([step('A', ['a', 'b']), step('B', ['c'])]);

    editing.dropItem({ step: 0, index: 1 }, { step: 1, index: 0 });

    expect(itemsOf(editing, 0)).toEqual(['a']);
    expect(itemsOf(editing, 1)).toEqual(['b', 'c']);
    expect(say).toHaveBeenCalledExactlyOnceWith('build.editor.dnd.transferred', { step: 2 });
  });

  it('refuses, silently, an item dropped into a full step', () => {
    const full = Array.from({ length: STEP_LIMITS.maxItemsPerStep }, () => 'x');
    const { editing, say } = setUp([step('A', full), step('B', ['y'])]);

    editing.dropItem({ step: 1, index: 0 }, { step: 0, index: 0 });

    expect(itemsOf(editing, 1)).toEqual(['y']);
    expect(say).not.toHaveBeenCalled();
  });

  it('announces a drag given up', () => {
    const { editing, say } = setUp();

    editing.announceDragCancelled();

    expect(say).toHaveBeenCalledExactlyOnceWith('build.editor.dnd.cancelled');
  });
});
