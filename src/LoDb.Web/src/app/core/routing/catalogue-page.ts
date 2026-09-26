import type { ResourceType } from '../api/generated/models/resource-type';
import type { Locale } from '../i18n/locales';

/** A requested list or detail of the catalogue, as the router cut it. */
export interface CataloguePage {
  readonly locale: Locale;
  /** Version segment of the path, or null when the URL follows the latest version. */
  readonly pinned: string | null;
  readonly resource: ResourceType;
  /** Entity segment of a detail (`Aatrox`, `1036-long-sword`), null on a list. */
  readonly entry: string | null;
  /** Query string with its `?`, or '': every redirect keeps it. */
  readonly query: string;
}
