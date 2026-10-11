import { BreakpointObserver } from '@angular/cdk/layout';
import { TestBed } from '@angular/core/testing';
import { DialogService } from '../../../../ui/overlays/dialog-service';
import { EditorCatalogs } from '../catalog/editor-catalogs';
import { StepEditing } from '../steps/step-editing';
import { ArmoryOpener } from './armory-opener';
import { ItemArmory } from './item-armory';

function opener(isPhone: boolean) {
  const dialogs = { open: vi.fn(), openSheet: vi.fn() };
  const editing = {};
  const catalogs = {};
  TestBed.configureTestingModule({
    providers: [
      ArmoryOpener,
      { provide: DialogService, useValue: dialogs },
      { provide: BreakpointObserver, useValue: { isMatched: () => isPhone } },
      { provide: StepEditing, useValue: editing },
      { provide: EditorCatalogs, useValue: catalogs },
    ],
  });
  // The armory opens out of the editor's injector: it is handed the order and the lists.
  const options = {
    labelledBy: ItemArmory.HEADING_ID,
    size: 'wide',
    data: { step: 1, editing, catalogs },
  };
  return { armory: TestBed.inject(ArmoryOpener), dialogs, options };
}

describe('ArmoryOpener', () => {
  it('opens the armory of a step as a dialog, from a tablet up', () => {
    const { armory, dialogs, options } = opener(false);

    armory.open(1);

    expect(dialogs.open).toHaveBeenCalledExactlyOnceWith(ItemArmory, options);
    expect(dialogs.openSheet).not.toHaveBeenCalled();
  });

  it('raises it as a bottom sheet on a phone', () => {
    const { armory, dialogs, options } = opener(true);

    armory.open(1);

    expect(dialogs.openSheet).toHaveBeenCalledExactlyOnceWith(ItemArmory, options);
    expect(dialogs.open).not.toHaveBeenCalled();
  });
});
