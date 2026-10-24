/** A link of the admin's navigation: its key under `admin.nav`, its path, its pages. */
export interface NavLink {
  readonly key: string;
  readonly path: string;
  /**
   * The paths on which the link is the current one, query and fragment left out: a period
   * or a filter stays on the same page.
   */
  readonly current: RegExp;
}

/** The glyph of a section of the sidebar (`NavIcon`). */
export type NavIconName = 'dashboard' | 'analytics' | 'management' | 'health';

/**
 * A section of the sidebar: its key under `admin.nav`, its glyph, its links. A section of a
 * single link is that link itself, its label the section's.
 */
export interface NavSection {
  readonly key: NavIconName;
  readonly links: readonly NavLink[];
}

/** The page the URL shows, and the section it belongs to. */
export interface NavPlace {
  readonly section: NavSection;
  readonly link: NavLink;
}

// The paths of a page and of the pages under it: /admin/robots, /admin/robots/googlebot.
const pageAndBelow = (path: string): RegExp => new RegExp(`^${path}(?:/.*)?$`);
const page = (path: string): RegExp => new RegExp(`^${path}/?$`);

/**
 * The four sections of the admin: the dashboard; what the site's traffic says (Analytics);
 * the accounts and what they send (Gestion); how the application holds up (Health).
 */
export const ADMIN_NAV: readonly NavSection[] = [
  {
    key: 'dashboard',
    links: [{ key: 'dashboard', path: '/admin', current: /^\/admin\/?$/ }],
  },
  {
    key: 'analytics',
    links: [
      { key: 'traffic', path: '/admin/traffic', current: page('/admin/traffic') },
      { key: 'robots', path: '/admin/robots', current: pageAndBelow('/admin/robots') },
      { key: 'audience', path: '/admin/audience', current: page('/admin/audience') },
    ],
  },
  {
    key: 'management',
    links: [
      { key: 'users', path: '/admin/users', current: page('/admin/users') },
      { key: 'contacts', path: '/admin/contacts', current: page('/admin/contacts') },
      { key: 'builds', path: '/admin/builds', current: page('/admin/builds') },
      { key: 'donations', path: '/admin/donations', current: page('/admin/donations') },
      { key: 'api_clients', path: '/admin/api-clients', current: page('/admin/api-clients') },
      // The activity of an account is a view of the journal.
      {
        key: 'journal',
        path: '/admin/journal',
        current: /^\/admin\/(journal|users\/[^/]+\/activity)\/?$/,
      },
    ],
  },
  {
    key: 'health',
    links: [
      { key: 'monitoring', path: '/admin/monitoring', current: page('/admin/monitoring') },
      { key: 'prerender', path: '/admin/prerender', current: page('/admin/prerender') },
      { key: 'storage', path: '/admin/storage', current: page('/admin/storage') },
      { key: 'backups', path: '/admin/backups', current: page('/admin/backups') },
    ],
  },
];

/** The page of a path, query and fragment left out; null off every link. */
export function navPlaceOf(path: string): NavPlace | null {
  for (const section of ADMIN_NAV) {
    const link = section.links.find((candidate) => candidate.current.test(path));
    if (link) {
      return { section, link };
    }
  }
  return null;
}
