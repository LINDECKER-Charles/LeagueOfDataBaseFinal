import { InjectionToken } from '@angular/core';
import type { LegalInfo } from './legal-info';

/**
 * The legal parameters, kept in configuration rather than in the texts: a change of host or
 * contact address is one line here, the same in both languages. Values of
 * `app/config/packages/legal_info.yaml`; what remains to check before production is listed in
 * docs/guides/legal-info.md.
 */
export const LEGAL_INFO = new InjectionToken<LegalInfo>('LEGAL_INFO', {
  providedIn: 'root',
  factory: () => ({
    siteName: 'League Of Data Base',
    siteUrl: 'https://www.league-of-data-base.com',
    publisherName: 'Charles LINDECKER',
    publisherStatus: 'Particulier (private individual)',
    publisherAddress: '',
    publisherEmail: 'charles.lindecker@outlook.fr',
    publicationDirector: 'Charles LINDECKER',
    hostName: 'Hostinger — HOSTINGER operations, UAB',
    hostAddress: 'Švitrigailos str. 34, 03230 Vilnius, Lituanie',
    hostPhone: '+370 645 03378',
    siret: 'N/A',
    dpoEmail: 'charles.lindecker@outlook.fr',
    jurisdictionCountry: 'France',
    effectiveDate: '2026-07-17',
  }),
});
