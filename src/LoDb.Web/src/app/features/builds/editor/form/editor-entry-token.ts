import { InjectionToken } from '@angular/core';
import type { EditorEntry } from '../editor-entry';

/**
 * The entry an editor instance works on, read once from its route: a new entry, a new
 * editor, so no state outlives the build it belonged to.
 */
export const EDITOR_ENTRY = new InjectionToken<EditorEntry>('EDITOR_ENTRY');
