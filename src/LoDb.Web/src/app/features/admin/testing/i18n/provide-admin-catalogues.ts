import type { EnvironmentProviders, Provider } from '@angular/core';
import { TRANSLOCO_LOADER, type Translation } from '@jsverse/transloco';
import { provideTranslocoMessageformat } from '@jsverse/transloco-messageformat';
import { of } from 'rxjs';

// Excerpts of `public/i18n/admin/{fr,en}.json`: the texts that change after an action.
const CATALOGUES: Readonly<Record<string, Translation>> = {
  'admin/fr': {
    actions: { refresh: 'Rafraîchir' },
    state: {
      unavailable: 'Panneau indisponible ({reason}).',
      reasons: { server: 'HTTP {status}' },
    },
    storage: { unavailable: 'Stockage indisponible : {error}' },
    monitoring: { counters: { unavailable: 'Compteurs indisponibles.' } },
    contacts: {
      handle: 'Marquer traité',
      reopen: 'Rouvrir',
      statuses: { new: 'Nouveau', handled: 'Traité' },
    },
    builds: {
      unpublish: 'Dépublier',
      visibilities: { public: 'public', private: 'privé' },
    },
    activity: {
      title: 'Activité — {name}',
      lede_orphan: 'Compte supprimé · actions effectuées par ce compte ou le ciblant.',
    },
  },
  'admin/en': {
    actions: { refresh: 'Refresh' },
    state: { unavailable: 'Panel unavailable ({reason}).', reasons: { server: 'HTTP {status}' } },
    storage: { unavailable: 'Storage unavailable: {error}' },
    monitoring: { counters: { unavailable: 'Counters unavailable.' } },
    contacts: {
      handle: 'Mark handled',
      reopen: 'Reopen',
      statuses: { new: 'New', handled: 'Handled' },
    },
    builds: {
      unpublish: 'Unpublish',
      visibilities: { public: 'public', private: 'private' },
    },
    activity: {
      title: 'Activity — {name}',
      lede_orphan: 'Deleted account · actions made by this account or targeting it.',
    },
  },
};

/**
 * Serves both catalogues of the `admin` scope, parsed as the site parses them, to a test
 * bed whose active language stays English: the admin must still read in French.
 */
export function provideAdminCatalogues(): (Provider | EnvironmentProviders)[] {
  return [
    {
      provide: TRANSLOCO_LOADER,
      useValue: { getTranslation: (path: string) => of(CATALOGUES[path] ?? {}) },
    },
    provideTranslocoMessageformat({ strictPluralKeys: false }),
  ];
}
