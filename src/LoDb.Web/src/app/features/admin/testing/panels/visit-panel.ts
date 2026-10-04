import { HttpStatusCode } from '@angular/common/http';
import type { TestRequest } from '@angular/common/http/testing';
import { sent } from '../http/sent';
import { openAdminPage } from '../open-admin-page';
import type { PanelCall } from './panel-call';
import type { PanelVisit } from './panel-visit';

/**
 * Opens `url` in the configured test bed and answers the calls the panel sends when it
 * opens, in turn. The page is stable, hence rendered, only once every call is answered.
 */
export async function visitPanel(url: string, calls: readonly PanelCall[]): Promise<PanelVisit> {
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
