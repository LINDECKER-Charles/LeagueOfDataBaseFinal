import type { EnvironmentProviders, Provider } from '@angular/core';
import type { PrerenderReport } from '../../../../../core/api/generated/models/prerender-report';
import { configureAdminTestBed } from '../../../testing/admin-test-bed';
import { prerenderPatches } from '../../../testing/fixtures/prerender-patches';
import { prerenderReport } from '../../../testing/fixtures/prerender-report';
import { scalewayBillReport } from '../../../testing/fixtures/scaleway-bill-report';
import { provideAdminCatalogues } from '../../../testing/i18n/provide-admin-catalogues';
import type { PanelCall } from '../../../testing/panels/panel-call';
import { panelRoute } from '../../../testing/panels/panel-route';
import type { PanelVisit } from '../../../testing/panels/panel-visit';
import { visitPanel } from '../../../testing/panels/visit-panel';
import { PrerenderPanel } from './prerender-panel';

/** How a spec opens the page of the pre-rendering. */
export interface PrerenderOpening {
  /** The report the page reads first; the fixture's running pass by default. */
  readonly report?: PrerenderReport;
  /** The calls the page sends besides the report, the coverage and the bill: a progress. */
  readonly calls?: readonly PanelCall[];
  /** Providers of the spec: a fake launch opener, a short cadence. */
  readonly providers?: readonly (Provider | EnvironmentProviders)[];
  /** Whether the admin's catalogues load: texts read in French, not as their keys. */
  readonly isFrench?: boolean;
}

const URL = '/admin/prerender';
const PRERENDER = '/api/admin/prerender';
const PATCHES = `${PRERENDER}/patches`;
// The Scaleway bill of the « Stockage et coûts » tab: every tab renders at once.
const BILL = '/api/admin/scaleway/bill';

/**
 * Opens `/admin/prerender` in a spec and answers what it reads on opening: the report, then
 * `calls` (the progress of a pass), the coverage of the served epoch and the bill.
 */
export function openPrerender(opening: PrerenderOpening = {}): Promise<PanelVisit> {
  const report = opening.report ?? prerenderReport();
  const catalogues = opening.isFrench ? provideAdminCatalogues() : [];
  configureAdminTestBed(
    [panelRoute(PrerenderPanel, URL)],
    [...catalogues, ...(opening.providers ?? [])],
  );
  return visitPanel(URL, [
    { path: PRERENDER, body: report },
    ...(opening.calls ?? []),
    { path: PATCHES, body: prerenderPatches() },
    { path: BILL, body: scalewayBillReport() },
  ]);
}
