import { SITE_IDENTITY } from '../site-identity';
import type { SeoPage } from '../seo-page';
import type { SeoTexts } from './seo-texts';

const SITE_SEPARATOR = ' — ';
const ACCOUNT_SEPARATOR = ' · ';
const ADMIN_SUFFIX = ' · Admin · LODB';

/**
 * The document title. A pinned version weaves its patch into the title, so a past page never
 * shares a title with the latest one: the difference keeps them out of a duplicate cluster.
 */
export function pageTitle(page: SeoPage, texts: SeoTexts): string {
  const title = page.title.trim();
  const format = page.titleFormat ?? (page.kind === 'private' ? 'account' : 'site');
  switch (format) {
    case 'raw':
      return title;
    case 'admin':
      return `${title}${ADMIN_SUFFIX}`;
    case 'account':
      return `${title}${ACCOUNT_SEPARATOR}${texts.siteTitle}`;
    case 'site': {
      const versioned = [title, texts.versionedSuffix ?? ''].join(' ').trim();
      return versioned === ''
        ? SITE_IDENTITY.name
        : `${versioned}${SITE_SEPARATOR}${SITE_IDENTITY.name}`;
    }
  }
}
