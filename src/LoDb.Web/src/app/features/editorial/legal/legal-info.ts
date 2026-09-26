/**
 * The site-identity facts the legal pages state verbatim. `publisherAddress` is empty on
 * purpose: under the non-professional publisher regime (LCEN art. 6, III-2) the notice
 * explains why no postal address is published.
 */
export interface LegalInfo {
  readonly siteName: string;
  readonly siteUrl: string;
  readonly publisherName: string;
  readonly publisherStatus: string;
  readonly publisherAddress: string;
  readonly publisherEmail: string;
  readonly publicationDirector: string;
  readonly hostName: string;
  readonly hostAddress: string;
  readonly hostPhone: string;
  readonly siret: string;
  readonly dpoEmail: string;
  readonly jurisdictionCountry: string;
  /** ISO calendar date the texts take effect, shown as "last updated". */
  readonly effectiveDate: string;
}
