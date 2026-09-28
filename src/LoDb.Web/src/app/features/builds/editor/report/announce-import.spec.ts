import { TestBed } from '@angular/core/testing';
import { TranslocoService } from '@jsverse/transloco';
import { ToastService } from '../../../../core/layout/toast/toast-service';
import type { EditorEntry } from '../editor-entry';
import { EDITOR_ENTRY } from '../form/editor-entry-token';
import { editorEntryOf } from '../testing/editor-entry-of';
import { announceImport } from './announce-import';

function announced(entry: Partial<EditorEntry>): unknown[][] {
  const show = vi.fn();
  TestBed.configureTestingModule({
    providers: [
      { provide: EDITOR_ENTRY, useValue: editorEntryOf({}, entry) },
      { provide: ToastService, useValue: { show } },
      { provide: TranslocoService, useValue: { translate: (key: string) => key } },
    ],
  });
  TestBed.runInInjectionContext(announceImport);
  return show.mock.calls;
}

describe('announceImport', () => {
  it('toasts where an import landed, then each thing to review as a warning', () => {
    const report = { championMissing: true, runesReset: false, droppedItems: [] };

    expect(announced({ report })).toEqual([
      ['success', 'build.import.done'],
      ['warning', 'build.import.champion_missing'],
    ]);
  });

  it('toasts nothing outside an import', () => {
    expect(announced({ report: null })).toEqual([]);
  });
});
