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

  it('adds the size modifier of the pane when the opener names one', () => {
    expect(dialogConfig({ labelledBy: 'contact-title', size: 'form' }).panelClass).toEqual([
      'hx-dialog',
      'hx-dialog--form',
    ]);
  });

  it('draws a viewer on a pane of its own, over a darker backdrop', () => {
    const lightbox = dialogConfig({ labelledBy: 'skin-title', size: 'lightbox' });
    const compact = dialogConfig({ labelledBy: 'chroma-title', size: 'compact' });

    expect(lightbox).toMatchObject({
      backdropClass: ['hx-backdrop', 'hx-backdrop--lightbox'],
      panelClass: 'hx-lightbox',
      autoFocus: 'dialog',
    });
    expect(compact).toMatchObject({
      backdropClass: ['hx-backdrop', 'hx-backdrop--compact'],
      panelClass: ['hx-lightbox', 'hx-lightbox--compact'],
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

  it('names its size on the sheet pane', () => {
    const position = createGlobalPositionStrategy(TestBed.inject(Injector)).bottom('0');

    const config = sheetConfig({ labelledBy: 'picker-title', size: 'picker' }, position);

    expect(config.panelClass).toEqual(['hx-sheet', 'hx-sheet--picker']);
  });
});
