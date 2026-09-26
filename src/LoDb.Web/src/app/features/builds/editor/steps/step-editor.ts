import { CdkDrag, CdkDragHandle, CdkDropList } from '@angular/cdk/drag-drop';
import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { TranslocoPipe } from '@jsverse/transloco';
import type { ItemOption } from '../../../../core/api/generated/models/item-option';
import { Button } from '../../../../ui/controls/button';
import { Image } from '../../../../ui/media/image';
import { EditorCatalogs } from '../catalog/editor-catalogs';
import { ArmoryOpener } from '../items/armory-opener';
import { imageSource } from '../shared/image-source';
import { initialsOf } from '../shared/initials-of';
import { STEP_LIMITS } from './order/step-limits';
import { StepDrag } from './step-drag';
import { StepEditing } from './step-editing';

// The labels the step label field suggests, `build.editor.steps.preset.<key>`.
const PRESETS = ['start', 'first_back', 'core', 'situational', 'final'] as const;

/**
 * The purchase order of the editor: its steps, each with a label, a note and its items.
 * Steps and items move by drag and drop, a press-and-hold on touch, and by buttons for the
 * keyboard and anyone who prefers them; items come from the armory.
 */
@Component({
  selector: 'lodb-step-editor',
  imports: [Button, CdkDrag, CdkDragHandle, CdkDropList, Image, TranslocoPipe],
  templateUrl: './step-editor.html',
  styleUrl: './step-editor.css',
  providers: [StepDrag, ArmoryOpener],
  host: { class: 'block space-y-5' },
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class StepEditor {
  protected readonly editing = inject(StepEditing);
  protected readonly drag = inject(StepDrag);
  private readonly armory = inject(ArmoryOpener);
  private readonly catalogs = inject(EditorCatalogs);
  protected readonly order = this.editing.order;
  protected readonly limits = STEP_LIMITS;
  protected readonly presets = PRESETS;
  /** A drag on touch starts after a press, so a swipe still scrolls the page. */
  protected readonly startDelay = { touch: 250, mouse: 0 };
  protected readonly imageSource = imageSource;
  protected readonly initialsOf = initialsOf;
  protected readonly goldOf = (itemId: string) => this.catalogs.resolveItem(itemId)?.gold ?? null;
  /** The item lists of every step, which an item may be dragged across. */
  protected readonly listIds = computed(() =>
    this.order().steps.map((_, index) => this.listId(index)),
  );

  protected listId(step: number): string {
    return `lodb-step-items-${step}`;
  }

  protected itemOf(itemId: string): ItemOption | undefined {
    return this.catalogs.resolveItem(itemId);
  }

  /** The translation key saying why an item is a ghost, null for an available one. */
  protected ghostKeyOf(itemId: string): string | null {
    switch (this.catalogs.ghostOf(itemId)) {
      case 'patch':
        return 'build.editor.ghost';
      case 'mode':
        return 'build.editor.ghost_mode';
      default:
        return null;
    }
  }

  protected openArmory(step: number): void {
    this.armory.open(step);
  }
}
