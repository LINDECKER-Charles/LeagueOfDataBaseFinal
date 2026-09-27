import { HttpStatusCode } from '@angular/common/http';
import type { TestRequest } from '@angular/common/http/testing';
import type { Type } from '@angular/core';
import type { Route } from '@angular/router';
import { configureAdminTestBed } from '../admin-test-bed';
import { sent } from '../http/sent';
import { openAdminPage } from '../open-admin-page';
import type { PanelCall } from './panel-call';
import type { PanelVisit } from './panel-visit';

/**
 * Opens a panel at `url` and answers the calls it sends when it opens, in turn. `panel` is
 * its route, or its component routed at the path of `url`. The page is stable, hence
 * rendered, only once every call is answered.
 */
export async function openPanel(
  panel: Type<unknown> | Route,
  url: string,
  calls: readonly PanelCall[],
): Promise<PanelVisit> {
  const [path = ''] = url.slice(1).split('?');
  configureAdminTestBed([typeof panel === 'function' ? { path, component: panel } : panel]);
  const answered: TestRequest[] = [];
  const visit = await openAdminPage(url, async (http) => {
    for (const call of calls) {
      const request = await sent({ http }, call.path);
      const status = call.status ?? HttpStatusCode.Ok;
      request.flush(call.body, { status, statusText: String(status) });
      answered.push(request);
    }
  });
  return { ...visit, calls: answered };
}
