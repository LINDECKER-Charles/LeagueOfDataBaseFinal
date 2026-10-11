import type { EnvironmentProviders, Provider } from '@angular/core';
import { TRANSLOCO_LOADER, TRANSLOCO_TRANSPILER, type Translation } from '@jsverse/transloco';
import { of } from 'rxjs';
import { PrecompiledTranspiler } from '../../../../core/i18n/messages/precompiled-transpiler';

// Excerpts of `public/i18n/admin/{fr,en}.json`, the texts that change after an action, as the
// build ships them (`npm run i18n:build`): a message with arguments comes compiled.
const CATALOGUES: Readonly<Record<string, Translation>> = {
  'admin/fr': {
    actions: { refresh: 'Rafraîchir' },
    state: {
      unavailable: ['Panneau indisponible (', ['arg', 'reason'], ').'],
      reasons: { server: ['HTTP ', ['arg', 'status']] },
    },
    storage: { unavailable: ['Stockage indisponible : ', ['arg', 'error']] },
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
      title: ['Activité — ', ['arg', 'name']],
      lede_orphan: 'Compte supprimé · actions effectuées par ce compte ou le ciblant.',
    },
  },
  'admin/en': {
    actions: { refresh: 'Refresh' },
    state: {
      unavailable: ['Panel unavailable (', ['arg', 'reason'], ').'],
      reasons: { server: ['HTTP ', ['arg', 'status']] },
    },
    storage: { unavailable: ['Storage unavailable: ', ['arg', 'error']] },
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
      title: ['Activity — ', ['arg', 'name']],
      lede_orphan: 'Deleted account · actions made by this account or targeting it.',
    },
  },
};

/**
 * Serves both catalogues of the `admin` scope, rendered as the site renders them, to a test
 * bed whose active language stays English: the admin must still read in French.
 */
export function provideAdminCatalogues(): (Provider | EnvironmentProviders)[] {
  return [
    {
      provide: TRANSLOCO_LOADER,
      useValue: { getTranslation: (path: string) => of(CATALOGUES[path] ?? {}) },
    },
    { provide: TRANSLOCO_TRANSPILER, useClass: PrecompiledTranspiler },
  ];
}
