/** A case of a plural or select, by key: `=1` is stored as `1`, a category as its name. */
type Cases = Readonly<Record<string, CompiledMessage>>;

type Part =
  | string
  // `{name}`: the parameter as it is.
  | readonly ['arg', name: string]
  // `#` inside a plural: the plural's parameter minus its offset, formatted for the locale.
  | readonly ['#', name: string, offset: number]
  | readonly ['plural' | 'selectordinal', name: string, offset: number, cases: Cases]
  | readonly ['select', name: string, cases: Cases];

/**
 * An ICU message as the build ships it (`npm run i18n:build`): parsed once, at build time,
 * into the parts a renderer concatenates, so that nothing compiles a message in the page
 * (no `new Function`, no `'unsafe-eval'`). An array in a catalogue is always one of these;
 * a message without ICU syntax stays a plain string.
 */
export type CompiledMessage = readonly Part[];
