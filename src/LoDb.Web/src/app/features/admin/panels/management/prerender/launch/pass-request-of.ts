import type { PassRequest } from '../../../../../../core/api/generated/models/pass-request';

// A pasted list of versions: one per line, or separated by commas, semicolons or spaces.
const SEPARATORS = /[\s,;]+/u;

function textOf(form: FormData, name: string): string {
  const value = form.get(name);
  return typeof value === 'string' ? value.trim() : '';
}

function priorityOf(text: string): string[] | null {
  // A version pasted twice keeps its first rank.
  const versions = [...new Set(text.split(SEPARATORS).filter((version) => version !== ''))];
  return versions.length > 0 ? versions : null;
}

/**
 * The pass the launch form asks for: a field left empty is not restricted, and an empty
 * epoch is the served one. The API checks the versions and the epoch, all of them at once.
 */
export function passRequestOf(form: FormData): PassRequest {
  const epoch = textOf(form, 'epoch');
  return {
    epoch: epoch === '' ? null : Number(epoch),
    oldest: textOf(form, 'oldest') || null,
    newest: textOf(form, 'newest') || null,
    pageType: textOf(form, 'pageType') || null,
    locale: textOf(form, 'locale') || null,
    priority: priorityOf(textOf(form, 'priority')),
  };
}
