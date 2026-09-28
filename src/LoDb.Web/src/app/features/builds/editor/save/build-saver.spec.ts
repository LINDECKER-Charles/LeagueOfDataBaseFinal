import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { TranslocoService } from '@jsverse/transloco';
import { API_BASE_URL } from '../../../../core/api/api-base-url';
import { ToastService } from '../../../../core/layout/toast/toast-service';
import type { EditorEntry } from '../editor-entry';
import { BuildEditorStore } from '../form/build-editor-store';
import { EDITOR_ENTRY } from '../form/editor-entry-token';
import { RuneDraft } from '../runes/rune-draft';
import { RuneEditing } from '../runes/rune-editing';
import { PurchaseOrder } from '../steps/order/purchase-order';
import { StepEditing } from '../steps/step-editing';
import { editorEntryOf } from '../testing/editor-entry-of';
import { BuildSaver } from './build-saver';

const API = 'https://api.example.com';
const STEPS = [{ label: 'Start', note: null, items: ['1001'] }];

function setUp(entry: Partial<EditorEntry> = {}) {
  const navigateByUrl = vi.fn(() => Promise.resolve(true));
  const show = vi.fn();
  TestBed.configureTestingModule({
    providers: [
      provideHttpClient(),
      provideHttpClientTesting(),
      { provide: API_BASE_URL, useValue: API },
      { provide: EDITOR_ENTRY, useValue: editorEntryOf({ championId: 'Ahri' }, entry) },
      { provide: RuneEditing, useValue: { page: signal(RuneDraft.empty()) } },
      { provide: StepEditing, useValue: { order: signal(PurchaseOrder.of(STEPS)) } },
      { provide: Router, useValue: { navigateByUrl } },
      { provide: ToastService, useValue: { show } },
      { provide: TranslocoService, useValue: { translate: (key: string) => key } },
      BuildEditorStore,
      BuildSaver,
    ],
  });
  const http = TestBed.inject(HttpTestingController);
  return {
    saver: TestBed.inject(BuildSaver),
    store: TestBed.inject(BuildEditorStore),
    http,
    navigateByUrl,
    show,
  };
}

const SAVED = { id: 7, shareToken: 'tok3n' };

describe('BuildSaver', () => {
  afterEach(() => TestBed.inject(HttpTestingController).verify());

  it('creates a new build as typed, then opens its share page', async () => {
    const { saver, store, http, navigateByUrl, show } = setUp();
    store.name.set('Burst mid');
    store.description.set('   ');

    const saving = saver.save();
    const request = http.expectOne(`${API}/api/builds?lang=en_US`);
    request.flush(SAVED);
    await saving;

    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({
      name: 'Burst mid',
      description: null,
      isPublic: false,
      gameVersion: '16.19.1',
      gameMode: 'sr',
      language: 'en_US',
      structure: {
        championId: 'Ahri',
        runes: RuneDraft.empty().toRunePage(),
        steps: STEPS,
      },
    });
    expect(show).toHaveBeenCalledExactlyOnceWith('success', 'build.flash.created');
    expect(navigateByUrl).toHaveBeenCalledExactlyOnceWith('/b/tok3n');
    expect(saver.saving()).toBe(false);
  });

  it('replaces the build it edits', async () => {
    const { saver, http, show } = setUp({ mode: 'edit', buildId: 7 });

    const saving = saver.save();
    const request = http.expectOne(`${API}/api/builds/7?lang=en_US`);
    request.flush(SAVED);
    await saving;

    expect(request.request.method).toBe('PUT');
    expect(show).toHaveBeenCalledExactlyOnceWith('success', 'build.flash.updated');
  });

  it('keeps the form on a refusal, each message of the API a toast', async () => {
    const { saver, http, navigateByUrl, show } = setUp();

    const saving = saver.save();
    http
      .expectOne(`${API}/api/builds?lang=en_US`)
      .flush(
        { code: 'validation-failed', errors: { name: ['name.length'], steps: ['steps.label'] } },
        { status: 400, statusText: 'Bad Request' },
      );
    await saving;

    expect(show.mock.calls).toEqual([
      ['error', 'build.error.name.length'],
      ['error', 'build.error.steps.label'],
    ]);
    expect(saver.saving()).toBe(false);
    expect(navigateByUrl).not.toHaveBeenCalled();
  });

  it('sends one save at a time', async () => {
    const { saver, http } = setUp();

    const saving = saver.save();
    await saver.save();
    http.expectOne(`${API}/api/builds?lang=en_US`).flush(SAVED);
    await saving;
  });
});
