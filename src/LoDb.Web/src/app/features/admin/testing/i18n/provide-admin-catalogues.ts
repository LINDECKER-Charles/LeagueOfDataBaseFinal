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
      states: { running: 'en cours', paused: 'en pause', served: 'servie', retired: 'retirée' },
      page_types: { items: 'objets' },
      overview: {
        cta: {
          launch: 'Lancer une passe',
          locked_active: ['Une passe est en cours (n°', ['arg', 'id'], ') : une seule à la fois.'],
        },
        views: {
          pass: {
            label: 'Passe',
            sub_running: ['n°', ['arg', 'id'], ' · epoch ', ['arg', 'epoch'], ' · en cours'],
          },
          coverage: {
            label: 'Couverture',
            sub: [
              'epoch ',
              ['arg', 'epoch'],
              ' servie · ',
              ['arg', 'complete'],
              ' / ',
              [
                'plural',
                'total',
                0,
                { one: [['#', 'total', 0], ' patch'], other: [['#', 'total', 0], ' patchs'] },
              ],
              ' complets',
            ],
          },
          queues: {
            label: 'Files automatiques',
            sub: [
              'en attente : ',
              ['arg', 'demand'],
              ' refusées (503) · ',
              ['arg', 'revision'],
              " d'états clos",
            ],
          },
          speed: {
            label: 'Vitesse',
            value_adaptive: 'Adaptative',
            sub_adaptive: ['entre ', ['arg', 'min'], ' et ', ['arg', 'max'], ' pages/min'],
          },
          storage: {
            label: 'Stockage et coûts',
            sub: [
              ['arg', 'cost'],
              ' / mois · ',
              [
                'plural',
                'pages',
                0,
                { one: [['#', 'pages', 0], ' page'], other: [['#', 'pages', 0], ' pages'] },
              ],
            ],
          },
          history: {
            label: 'Historique',
            value: [
              [
                'plural',
                'count',
                0,
                {
                  0: ['Aucune'],
                  one: [['#', 'count', 0], ' passe'],
                  other: [['#', 'count', 0], ' passes'],
                },
              ],
            ],
            sub: ['dernière : n°', ['arg', 'id'], ' (', ['arg', 'state'], '), ', ['arg', 'date']],
          },
        },
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
      states: { running: 'running', paused: 'paused', served: 'served', retired: 'retired' },
      page_types: { items: 'items' },
      overview: {
        cta: {
          launch: 'Launch a pass',
          locked_active: ['A pass is running (#', ['arg', 'id'], '): one at a time.'],
        },
        kpis: {
          cost_sub_retired: [
            '+ ',
            ['arg', 'retired'],
            ' for the retired epochs, until deleted; egress left out',
          ],
        },
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
