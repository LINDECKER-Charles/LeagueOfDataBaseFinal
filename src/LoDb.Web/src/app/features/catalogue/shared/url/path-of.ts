const PATH_END = /[?#]/;

/** The path of a root-relative URL, without its query and fragment. */
export function pathOf(url: string): string {
  return url.split(PATH_END, 1)[0];
}
