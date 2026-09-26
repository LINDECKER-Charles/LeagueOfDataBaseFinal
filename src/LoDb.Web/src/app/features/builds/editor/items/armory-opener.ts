import { BreakpointObserver } from '@angular/cdk/layout';
import { Injectable, inject } from '@angular/core';
import { DialogService } from '../../../../ui/overlays/dialog-service';
import { EditorCatalogs } from '../catalog/editor-catalogs';
import { StepEditing } from '../steps/step-editing';
import type { ArmoryData } from './armory-data';
import { ItemArmory } from './item-armory';

// Below the `sm` breakpoint the armory rises as a bottom sheet, within thumb reach.
const PHONE = '(max-width: 639.98px)';

/** Opens the armory of a step: a dialog, a bottom sheet on a phone. */
@Injectable()
export class ArmoryOpener {
  private readonly dialogs = inject(DialogService);
  private readonly breakpoints = inject(BreakpointObserver);
  private readonly editing = inject(StepEditing);
  private readonly catalogs = inject(EditorCatalogs);

  open(step: number): void {
    const options = {
      labelledBy: ItemArmory.HEADING_ID,
      data: { step, editing: this.editing, catalogs: this.catalogs } satisfies ArmoryData,
    };
    if (this.breakpoints.isMatched(PHONE)) {
      this.dialogs.openSheet(ItemArmory, options);
    } else {
      this.dialogs.open(ItemArmory, options);
    }
  }
}
