import type { ResourceType } from '../api/generated/models/resource-type';
import type { Locale } from '../i18n/locales';
import type { PATCH_CHANGES_PATH } from './patch-changes-path';

/**
 * A requested list or detail of the catalogue, or the page of a patch's changes, which
 * follows the URL rules of a list: as the router cut it.
 */
export interface CataloguePage {
  readonly locale: Locale;
  /** Version segment of the path, or null when the URL follows the latest version. */
  readonly pinned: string | null;
  /** The resource, or the page of a patch's changes. */
  readonly resource: ResourceType | typeof PATCH_CHANGES_PATH;
  /** Entity segment of a detail (`Aatrox`, `1036-long-sword`), null on a list. */
  readonly entry: string | null;
  /** Query string with its `?`, or '': every redirect keeps it. */
  readonly query: string;
}
