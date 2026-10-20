import type { PassRequest } from '../../../../../../core/api/generated/models/pass-request';

// A pasted list of versions: one per line, or separated by commas, semicolons or spaces.
const SEPARATORS = /[\s,;]+/u;

function textOf(form: FormData, name: string): string {
  const value = form.get(name);
  return typeof value === 'string' ? value.trim() : '';
}

function listOf(text: string): string[] | null {
  // A version pasted twice keeps its first rank.
  const versions = [...new Set(text.split(SEPARATORS).filter((version) => version !== ''))];
  return versions.length > 0 ? versions : null;
}

function oneOf(text: string): string[] | null {
  return text === '' ? null : [text];
}

/**
 * The pass the launch form asks for: a field left empty is not restricted, and an empty
 * epoch is the served one. The one type and the one locale picked go as lists of one. The API
 * checks the versions and the epoch, all of them at once.
 */
export function passRequestOf(form: FormData): PassRequest {
  const epoch = textOf(form, 'epoch');
  return {
    epoch: epoch === '' ? null : Number(epoch),
    versions: listOf(textOf(form, 'versions')),
    locales: oneOf(textOf(form, 'locale')),
    pageTypes: oneOf(textOf(form, 'pageType')),
    priority: listOf(textOf(form, 'priority')),
    skipComplete: false,
  };
}
