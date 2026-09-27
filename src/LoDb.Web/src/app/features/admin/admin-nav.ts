/** A link of the admin's navigation: its key under `admin.nav` and its path. */
interface NavLink {
  readonly key: string;
  readonly path: string;
  /** Active on its own path only, not on the pages below it. */
  readonly exact: boolean;
}

/** A group of links under a heading, as the legacy sidebar had them. */
interface NavSection {
  readonly key: string;
  readonly links: readonly NavLink[];
}

/** The sections of the admin, in the order of the legacy sidebar. */
export const ADMIN_NAV: readonly NavSection[] = [
  {
    key: 'analytics',
    links: [
      { key: 'overview', path: '/admin', exact: true },
      { key: 'traffic', path: '/admin/traffic', exact: false },
      { key: 'audience', path: '/admin/audience', exact: false },
      { key: 'storage', path: '/admin/storage', exact: false },
    ],
  },
  {
    key: 'management',
    links: [
      { key: 'users', path: '/admin/users', exact: false },
      { key: 'builds', path: '/admin/builds', exact: false },
      { key: 'donations', path: '/admin/donations', exact: false },
      { key: 'contacts', path: '/admin/contacts', exact: false },
      { key: 'api_clients', path: '/admin/api-clients', exact: false },
      { key: 'monitoring', path: '/admin/monitoring', exact: false },
      { key: 'journal', path: '/admin/journal', exact: false },
    ],
  },
];
