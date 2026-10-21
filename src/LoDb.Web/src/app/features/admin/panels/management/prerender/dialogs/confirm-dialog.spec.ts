import { ChangeDetectionStrategy, Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { configureAdminTestBed } from '../../../../testing/admin-test-bed';
import type { ConfirmData } from './confirm-data';
import { injectAdminDialogs } from './inject-admin-dialogs';

const PAUSE: ConfirmData = {
  title: 'Mettre la passe n°12 en pause ?',
  lines: ['16.18.1 · fr s’arrête à 312 / 1 140.', 'La passe reprendra exactement là.'],
  confirmLabel: 'Mettre en pause',
  keepLabel: 'Laisser tourner',
  tone: 'gold',
};

@Component({ template: '', changeDetection: ChangeDetectionStrategy.OnPush })
class Host {
  readonly dialogs = injectAdminDialogs();
}

function overlay(): HTMLElement {
  const container = document.querySelector<HTMLElement>('.cdk-overlay-container');
  if (!container) {
    throw new Error('No dialog is open.');
  }
  return container;
}

function button(selector: string): HTMLButtonElement {
  const found = overlay().querySelector<HTMLButtonElement>(selector);
  if (!found) {
    throw new Error(`No button matches ${selector}.`);
  }
  return found;
}

// Opens the question; its answer is wrapped, or awaiting the opening would await it too.
async function ask(): Promise<{ readonly answer: Promise<boolean> }> {
  const fixture = TestBed.createComponent(Host);
  const answer = fixture.componentInstance.dialogs.confirm(PAUSE);
  fixture.detectChanges();
  await fixture.whenStable();
  await new Promise((resolve) => setTimeout(resolve));
  return { answer };
}

describe('ConfirmDialog', () => {
  beforeEach(() => configureAdminTestBed());
  afterEach(() => document.querySelector('.cdk-overlay-container')?.replaceChildren());

  it('says the consequences, in an admin overlay, the safe choice focused', async () => {
    await ask();

    const words = (overlay().textContent ?? '').replace(/\s+/g, ' ');
    expect(words).toContain('Mettre la passe n°12 en pause ?');
    expect(words).toContain('La passe reprendra exactement là.');
    expect(overlay().querySelector('lodb-confirm-dialog')?.classList).toContain('admin-overlay');
    expect(document.activeElement).toBe(button('[data-confirm-keep]'));
    expect(button('[data-confirm]').className).toContain('hx-btn-gold');
  });

  it('answers true once confirmed', async () => {
    const { answer } = await ask();

    button('[data-confirm]').click();
    await expect(answer).resolves.toBe(true);
  });

  it('answers false once kept, and on Escape', async () => {
    const { answer: kept } = await ask();
    button('[data-confirm-keep]').click();
    await expect(kept).resolves.toBe(false);

    const { answer: escaped } = await ask();
    document.body.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape', keyCode: 27 }));
    await expect(escaped).resolves.toBe(false);
  });
});
