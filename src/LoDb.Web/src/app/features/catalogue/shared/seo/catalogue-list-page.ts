import type { PageContext } from '../../../../core/context/page-context';

/** What the head of a list page is made of, its texts already translated. */
export interface CatalogueListPage {
  readonly title: string;
  readonly description: string;
  /** The list's path below the locale and version, such as `champions`. */
  readonly path: string;
  readonly context: PageContext;
  /** The entries of the page the server rendered, in order. */
  readonly entries: readonly { readonly name: string; readonly canonicalPath: string }[];
}
