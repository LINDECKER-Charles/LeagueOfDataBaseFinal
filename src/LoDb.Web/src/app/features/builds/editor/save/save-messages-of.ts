import { HttpStatusCode } from '@angular/common/http';
import type { EditorMessage } from '../shared/editor-message';
import type { BuildProblem } from './build-problem';

// The codes of a refused build (the API's BuildErrors), each read as `build.error.{code}`.
const KNOWN_CODES: ReadonlySet<string> = new Set([
  'name.length',
  'description.length',
  'mode.unknown',
  'version.unknown',
  'language.unknown',
  'structure.invalid',
  'champion.unknown',
  'runes.primary_style',
  'runes.primary_selection_count',
  'runes.primary_selection_slot',
  'runes.secondary_style',
  'runes.secondary_same_style',
  'runes.secondary_selection_count',
  'runes.secondary_selection_slot',
  'runes.secondary_same_slot',
  'steps.count',
  'steps.label',
  'steps.note',
  'steps.items_count',
  'steps.item_unknown',
  'steps.total_items',
  'steps.item_mode',
]);
const ITEM_MODE = 'steps.item_mode';
const FALLBACK_CODE = 'structure.invalid';

// The refusals that are no field's: each says one thing, whatever the fields.
const BY_CODE: Readonly<Record<string, string>> = {
  'authentication-required': 'buildsEditor.errors.signed_out',
  'email-not-verified': 'buildsEditor.errors.email_unverified',
  'xsrf-invalid': 'build.error.csrf',
  'build-not-found': 'buildsEditor.errors.not_found',
};

function fieldMessagesOf(problem: BuildProblem): EditorMessage[] {
  const codes = new Set(Object.values(problem.errors).flat());
  return [...codes].map((code) => {
    if (code === ITEM_MODE) {
      return { key: `build.error.${code}`, params: { items: problem.unavailableItems.join(', ') } };
    }
    return { key: `build.error.${KNOWN_CODES.has(code) ? code : FALLBACK_CODE}` };
  });
}

/**
 * The messages over a refused save, translation keys shown as the API worded them: each
 * code of each invalid field, the items the mode excludes named; else the one message of
 * the refusal. An unknown field code reads as an invalid structure, never as nothing.
 */
export function saveMessagesOf(problem: BuildProblem): EditorMessage[] {
  if (problem.code === 'validation-failed') {
    const messages = fieldMessagesOf(problem);
    return messages.length > 0 ? messages : [{ key: `build.error.${FALLBACK_CODE}` }];
  }
  const known = problem.code === null ? undefined : BY_CODE[problem.code];
  if (known !== undefined) {
    return [{ key: known }];
  }
  if (problem.status === HttpStatusCode.ServiceUnavailable) {
    return [{ key: 'build.error.catalog_unavailable' }];
  }
  if (problem.status === HttpStatusCode.Unauthorized) {
    return [{ key: 'buildsEditor.errors.signed_out' }];
  }
  return [{ key: 'buildsEditor.errors.generic' }];
}
