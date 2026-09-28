import type { IconName } from '../../../ui/media/icon-name';

/** A footer link: an absolute URL outside the site, or a path under the locale inside it. */
export interface FooterLink {
  readonly href: string;
  /** Translation key of the label. */
  readonly label: string;
  readonly icon?: IconName;
}
