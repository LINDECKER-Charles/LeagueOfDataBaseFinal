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
    backups: {
      databases: { lodb: 'LoDb (production)', umami: 'Umami (audience)' },
      statuses: { ok: 'à jour', late: 'en retard', failed: 'en échec' },
    },
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
    prerender: {
      ready: [
        "L'epoch ",
        ['arg', 'epoch'],
        ' est complète. Pour la servir, posez LODB_ARCHIVE_SERVE_EPOCH=',
        ['arg', 'epoch'],
        " et redéployez : nginx ne change pas d'epoch à chaud.",
      ],
      states: { running: 'en cours', paused: 'en pause', served: 'servie', retired: 'retirée' },
      page_types: { items: 'objets' },
      current: { pause: 'Mettre en pause', resume: 'Reprendre' },
      kpi: {
        cost_sub_retired: [
          '+ ',
          ['arg', 'retired'],
          " pour les epochs retirées, jusqu'à leur suppression ; hors sortie",
        ],
      },
    },
    scaleway_bill: {
      stale: [
        "Scaleway n'a pas répondu à la dernière lecture : montants lus le ",
        ['arg', 'date'],
        '.',
      ],
      fetched: ['Relevé chez Scaleway le ', ['arg', 'date'], '.'],
      units: { gigabyte_hour: 'Go·h' },
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
    backups: {
      databases: { lodb: 'LoDb (production)', umami: 'Umami (audience)' },
      statuses: { ok: 'up to date', late: 'late', failed: 'failed' },
    },
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
    prerender: {
      ready: [
        'Epoch ',
        ['arg', 'epoch'],
        ' is complete. To serve it, set LODB_ARCHIVE_SERVE_EPOCH=',
        ['arg', 'epoch'],
        ' and redeploy: nginx does not switch epochs on the fly.',
      ],
      states: { running: 'running', paused: 'paused', served: 'served', retired: 'retired' },
      page_types: { items: 'items' },
      current: { pause: 'Pause', resume: 'Resume' },
      kpi: {
        cost_sub_retired: [
          '+ ',
          ['arg', 'retired'],
          ' for the retired epochs, until their deletion; egress excluded',
        ],
      },
    },
    scaleway_bill: {
      stale: ['Scaleway did not answer the last reading: amounts read on ', ['arg', 'date'], '.'],
      fetched: ['Read from Scaleway on ', ['arg', 'date'], '.'],
      units: { gigabyte_hour: 'GB·h' },
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
