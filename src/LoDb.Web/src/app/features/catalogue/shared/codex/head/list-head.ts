import type { PageContext } from '../../../../../core/context/page-context';

/** What the head of a list page holds, once its list is known. */
export interface ListHead {
  readonly context: PageContext;
  /** Prefix of its SEO texts, such as `item` for `item.list.title`. */
  readonly texts: 'item' | 'rune' | 'summoner';
  /** The list's path, which also names its preview image: `items`. */
  readonly path: string;
  /** Entries of the whole list; null when it could not be read. */
  readonly count: number | null;
  /** The pages the list links to first, in order. */
  readonly entries: readonly { readonly name: string; readonly canonicalPath: string }[];
}
