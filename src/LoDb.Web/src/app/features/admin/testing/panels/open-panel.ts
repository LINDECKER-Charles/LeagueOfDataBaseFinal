import type { Type } from '@angular/core';
import type { Route } from '@angular/router';
import { configureAdminTestBed } from '../admin-test-bed';
import type { PanelCall } from './panel-call';
import { panelRoute } from './panel-route';
import type { PanelVisit } from './panel-visit';
import { visitPanel } from './visit-panel';

/**
 * Opens a panel at `url` and answers the calls it sends when it opens, in turn. `panel` is
 * its route, or its component routed at the path of `url`. The page is stable, hence
 * rendered, only once every call is answered.
 */
export function openPanel(
  panel: Type<unknown> | Route,
  url: string,
  calls: readonly PanelCall[],
): Promise<PanelVisit> {
  configureAdminTestBed([panelRoute(panel, url)]);
  return visitPanel(url, calls);
}
