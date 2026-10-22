import type { Translation } from '@jsverse/transloco';

const PARAMETER = /{{\s*(\w+)\s*}}/g;

/**
 * A translation of a catalogue as the authored file gives it, for the specs of pure text
 * builders: a key's text with its `{{ params }}` filled, the key itself when the catalogue
 * lacks it, as Transloco answers a missing key.
 */
export function translateFrom(
  catalogue: Translation,
): (key: string, params?: Readonly<Record<string, unknown>>) => string {
  return (key, params = {}) => {
    const text = key
      .split('.')
      .reduce<unknown>(
        (node, part) =>
          node !== null && typeof node === 'object' ? (node as Translation)[part] : undefined,
        catalogue,
      );
    return typeof text === 'string'
      ? text.replace(PARAMETER, (_match, name: string) => String(params[name] ?? ''))
      : key;
  };
}
