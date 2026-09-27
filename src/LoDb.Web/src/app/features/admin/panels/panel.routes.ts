import type { Routes } from '@angular/router';

/**
 * The panels of the admin under its shell, each in its own chunk, loaded when opened. The
 * overview has no route: the shell shows it at `/admin`.
 */
export const PANEL_ROUTES: Routes = [
  {
    path: 'traffic',
    loadComponent: () => import('./analytics/traffic/traffic-panel').then((m) => m.TrafficPanel),
  },
  {
    path: 'audience',
    loadComponent: () => import('./analytics/audience/audience-panel').then((m) => m.AudiencePanel),
  },
  {
    path: 'storage',
    loadComponent: () => import('./analytics/storage/storage-panel').then((m) => m.StoragePanel),
  },
  {
    path: 'users',
    loadComponent: () => import('./management/users/users-panel').then((m) => m.UsersPanel),
  },
  {
    path: 'users/:id/activity',
    loadComponent: () =>
      import('./management/users/user-activity-panel').then((m) => m.UserActivityPanel),
  },
  {
    path: 'builds',
    loadComponent: () => import('./management/builds/builds-panel').then((m) => m.BuildsPanel),
  },
  {
    path: 'donations',
    loadComponent: () =>
      import('./management/donations/donations-panel').then((m) => m.DonationsPanel),
  },
  {
    path: 'contacts',
    loadComponent: () =>
      import('./management/contacts/contacts-panel').then((m) => m.ContactsPanel),
  },
  {
    path: 'api-clients',
    loadComponent: () =>
      import('./management/api-clients/api-clients-panel').then((m) => m.ApiClientsPanel),
  },
  {
    path: 'monitoring',
    loadComponent: () =>
      import('./management/monitoring/monitoring-panel').then((m) => m.MonitoringPanel),
  },
  {
    path: 'journal',
    loadComponent: () => import('./management/journal/journal-panel').then((m) => m.JournalPanel),
  },
];
