import { createGlobalPositionStrategy } from '@angular/cdk/overlay';
import { Injector } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { dialogConfig } from './dialog-config';
import { sheetConfig } from './sheet-config';

describe('dialogConfig', () => {
  it('skins the pane and the backdrop, and names the dialog by its heading', () => {
    expect(dialogConfig({ labelledBy: 'theme-title', data: { theme: 'zaun' } })).toEqual({
      ariaLabelledBy: 'theme-title',
      ariaModal: true,
      autoFocus: 'dialog',
      backdropClass: 'hx-backdrop',
      data: { theme: 'zaun' },
      maxWidth: '100vw',
      panelClass: 'hx-dialog',
      restoreFocus: true,
    });
  });
});

describe('sheetConfig', () => {
  it('keeps the dialog behaviour, full width at the bottom edge', () => {
    const position = createGlobalPositionStrategy(TestBed.inject(Injector)).bottom('0');

    const config = sheetConfig({ labelledBy: 'filters-title' }, position);

    expect(config).toMatchObject({
      ariaLabelledBy: 'filters-title',
      ariaModal: true,
      backdropClass: 'hx-backdrop',
      panelClass: 'hx-sheet',
      width: '100%',
    });
    expect(config.positionStrategy).toBe(position);
  });
});
