import { Component } from '@angular/core';
import { openPanel } from '../../../../testing/panels/open-panel';
import { TrafficExplorer } from './traffic-explorer';
import { explorerRows } from './explorer-rows';

const BREAKDOWN = '/api/admin/analytics/breakdown';

@Component({
  imports: [TrafficExplorer],
  template: '<lodb-traffic-explorer range="7d" />',
})
class Host {}

const ENTITIES = {
  range: '7d',
  dimension: 'entities',
  total: 500,
  matched: 51,
  rows: [{ name: 'champion:Ahri', count: 300, pct: 60 }],
  page: 1,
  hasMore: true,
};

describe('TrafficExplorer', () => {
  it('asks the dimension, the search and the page of the URL, an entity named by its key', async () => {
    const { page, calls } = await openPanel(Host, '/admin/traffic?dim=entities&q=ahri', [
      { path: BREAKDOWN, body: ENTITIES },
    ]);

    const params = calls[0]?.request.params;
    expect([params?.get('range'), params?.get('dimension'), params?.get('q')]).toEqual([
      '7d',
      'entities',
      'ahri',
    ]);
    expect(page.querySelector('tbody td')?.textContent).toContain('Ahri');
    expect(page.querySelector('tbody td .cell-sub')?.textContent?.trim()).toBe('champion');
    expect(page.querySelector('lodb-admin-pager a')?.getAttribute('href')).toContain('page=2');
  });

  it('keeps a page or a host as it is', () => {
    const texts = { text: (key: string) => key, term: (_: string, name: string) => name };

    const rows = explorerRows('referers', [{ name: 'www.google.fr', count: 1, pct: 1 }], texts);

    expect(rows[0]).toEqual({
      key: 'www.google.fr',
      name: 'www.google.fr',
      kind: '',
      count: 1,
      pct: 1,
    });
  });
});
