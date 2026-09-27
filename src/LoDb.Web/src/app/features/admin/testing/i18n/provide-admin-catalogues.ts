import type { EnvironmentProviders, Provider } from '@angular/core';
import { TRANSLOCO_LOADER, type Translation } from '@jsverse/transloco';
import { provideTranslocoMessageformat } from '@jsverse/transloco-messageformat';
import { of } from 'rxjs';

// Excerpts of `public/i18n/admin/{fr,en}.json`: the texts that change after an action.
const CATALOGUES: Readonly<Record<string, Translation>> = {
  'admin/fr': {
    actions: { refresh: 'Actualiser' },
    common: { generated_at: 'Relevé du {at} (UTC)' },
    contacts: { handle: 'Marquer traité', reopen: 'Rouvrir', handled_at: 'Traité le {at}' },
    builds: {
      unpublish: 'Dépublier',
      confirm_unpublish: 'Confirmer la dépublication',
      visibilities: { public: 'Public', private: 'Privé' },
    },
  },
  'admin/en': {
    actions: { refresh: 'Refresh' },
    common: { generated_at: 'Read at {at} (UTC)' },
    contacts: { handle: 'Mark handled', reopen: 'Reopen', handled_at: 'Handled on {at}' },
    builds: {
      unpublish: 'Unpublish',
      confirm_unpublish: 'Confirm unpublishing',
      visibilities: { public: 'Public', private: 'Private' },
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
