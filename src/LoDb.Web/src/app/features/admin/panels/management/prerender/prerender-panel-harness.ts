import type { EnvironmentProviders, Provider } from '@angular/core';
import type { PrerenderReport } from '../../../../../core/api/generated/models/prerender-report';
import { configureAdminTestBed } from '../../../testing/admin-test-bed';
import { prerenderDemand, prerenderDemandPages } from '../../../testing/fixtures/prerender-demand';
import { prerenderPatches } from '../../../testing/fixtures/prerender-patches';
import { prerenderReport } from '../../../testing/fixtures/prerender-report';
import { prerenderSpeed } from '../../../testing/fixtures/prerender-speed';
import { prerenderRevision } from '../../../testing/fixtures/revision/prerender-revision';
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
  /** The URL opened, its query included: `/admin/prerender?view=history`. */
  readonly url?: string;
}

const PAGE = '/admin/prerender';
const PRERENDER = '/api/admin/prerender';
const PATCHES = `${PRERENDER}/patches`;
// The Scaleway bill of the « Stockage et coûts » view: every view renders at once.
const BILL = '/api/admin/scaleway/bill';
// The written pages of the automatic queue, listed by its view.
const DEMAND_PAGES = `${PRERENDER}/demand/pages`;

/**
 * What the page reads as it opens, before its report answers: the automatic queue, the queue
 * of the closed states and the speed, which the tiles of the views show.
 */
export const BESIDE_THE_REPORT: readonly PanelCall[] = [
  { path: `${PRERENDER}/demand`, body: prerenderDemand() },
  { path: `${PRERENDER}/revision`, body: prerenderRevision() },
  { path: `${PRERENDER}/speed`, body: prerenderSpeed() },
];

/**
 * Opens `/admin/prerender` in a spec and answers what it reads on opening: the report, then
 * `calls` (the progress of a pass), the coverage of the served epoch, the bill, the written
 * pages of the automatic queue, and the reads beside the report.
 */
export function openPrerender(opening: PrerenderOpening = {}): Promise<PanelVisit> {
  const report = opening.report ?? prerenderReport();
  const catalogues = opening.isFrench ? provideAdminCatalogues() : [];
  configureAdminTestBed(
    [panelRoute(PrerenderPanel, PAGE)],
    [...catalogues, ...(opening.providers ?? [])],
  );
  return visitPanel(opening.url ?? PAGE, [
    { path: PRERENDER, body: report },
    ...(opening.calls ?? []),
    { path: PATCHES, body: prerenderPatches() },
    { path: BILL, body: scalewayBillReport() },
    { path: DEMAND_PAGES, body: prerenderDemandPages() },
    ...BESIDE_THE_REPORT,
  ]);
}
