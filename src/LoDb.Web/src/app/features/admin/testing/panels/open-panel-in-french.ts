import type { Type } from '@angular/core';
import type { Route } from '@angular/router';
import { configureAdminTestBed } from '../admin-test-bed';
import { provideAdminCatalogues } from '../i18n/provide-admin-catalogues';
import type { PanelCall } from './panel-call';
import { panelRoute } from './panel-route';
import type { PanelVisit } from './panel-visit';
import { visitPanel } from './visit-panel';

/**
 * Opens a panel as `openPanel` does, with the catalogues of the admin in French and in
 * English while the site stays in English: its texts must read in French all the same.
 */
export function openPanelInFrench(
  panel: Type<unknown> | Route,
  url: string,
  calls: readonly PanelCall[],
): Promise<PanelVisit> {
  configureAdminTestBed([panelRoute(panel, url)], provideAdminCatalogues());
  return visitPanel(url, calls);
}
