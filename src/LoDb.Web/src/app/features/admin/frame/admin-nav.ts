/** A link of the admin's navigation: its key under `admin.nav`, its path, its pages. */
interface NavLink {
  readonly key: string;
  readonly path: string;
  /**
   * The paths on which the link is the current one, query and fragment left out: a period
   * or a filter stays on the same page.
   */
  readonly current: RegExp;
}

/** A group of links under a label, as the legacy top bar had them. */
interface NavSection {
  readonly key: string;
  readonly links: readonly NavLink[];
}

/** The sections of the admin, in the order of the legacy top bar. */
export const ADMIN_NAV: readonly NavSection[] = [
  {
    key: 'analytics',
    links: [
      { key: 'overview', path: '/admin', current: /^\/admin\/?$/ },
      { key: 'traffic', path: '/admin/traffic', current: /^\/admin\/traffic\/?$/ },
      { key: 'audience', path: '/admin/audience', current: /^\/admin\/audience\/?$/ },
      { key: 'storage', path: '/admin/storage', current: /^\/admin\/storage\/?$/ },
    ],
  },
  {
    key: 'management',
    links: [
      { key: 'users', path: '/admin/users', current: /^\/admin\/users\/?$/ },
      { key: 'builds', path: '/admin/builds', current: /^\/admin\/builds\/?$/ },
      { key: 'donations', path: '/admin/donations', current: /^\/admin\/donations\/?$/ },
      { key: 'contacts', path: '/admin/contacts', current: /^\/admin\/contacts\/?$/ },
      { key: 'api_clients', path: '/admin/api-clients', current: /^\/admin\/api-clients\/?$/ },
      { key: 'monitoring', path: '/admin/monitoring', current: /^\/admin\/monitoring\/?$/ },
      // The activity of an account is a view of the journal, as the legacy bar had it.
      {
        key: 'journal',
        path: '/admin/journal',
        current: /^\/admin\/(journal|users\/[^/]+\/activity)\/?$/,
      },
    ],
  },
];
