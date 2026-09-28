/** A list of a picker: on its way, unreadable (the dialog offers to try again), or there. */
export type PickerLoad<T> =
  | { readonly status: 'loading' }
  | { readonly status: 'failed' }
  | { readonly status: 'ready'; readonly entries: readonly T[] };
