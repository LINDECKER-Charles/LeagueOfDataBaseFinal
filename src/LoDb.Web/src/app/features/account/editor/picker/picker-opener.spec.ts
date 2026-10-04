import { BreakpointObserver } from '@angular/cdk/layout';
import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { DialogService } from '../../../../ui/overlays/dialog-service';
import { PickerOpener } from './picker-opener';

@Component({ template: '' })
class Picker {}

function opener(isPhone: boolean) {
  const dialogs = { open: vi.fn(), openSheet: vi.fn() };
  TestBed.configureTestingModule({
    providers: [
      PickerOpener,
      { provide: DialogService, useValue: dialogs },
      { provide: BreakpointObserver, useValue: { isMatched: () => isPhone } },
    ],
  });
  return { pickers: TestBed.inject(PickerOpener), dialogs };
}

// The legacy picker sheet: 32rem centred from `md`, a bottom sheet below, its search focused.
const OPTIONS = {
  labelledBy: 'picker-title',
  data: { slot: 'champion' },
  size: 'picker',
  autoFocus: 'input[type=search]',
};

describe('PickerOpener', () => {
  it('opens a picker dialog, its search focused, from a tablet up', () => {
    const { pickers, dialogs } = opener(false);

    pickers.open(Picker, 'picker-title', { slot: 'champion' });

    expect(dialogs.open).toHaveBeenCalledExactlyOnceWith(Picker, OPTIONS);
    expect(dialogs.openSheet).not.toHaveBeenCalled();
  });

  it('raises a bottom sheet on a phone', () => {
    const { pickers, dialogs } = opener(true);

    pickers.open(Picker, 'picker-title', { slot: 'champion' });

    expect(dialogs.openSheet).toHaveBeenCalledExactlyOnceWith(Picker, OPTIONS);
    expect(dialogs.open).not.toHaveBeenCalled();
  });
});
