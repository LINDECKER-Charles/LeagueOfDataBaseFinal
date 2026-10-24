import { robotDetail } from '../../../../testing/fixtures/robot-report';
import { openPanel } from '../../../../testing/panels/open-panel';
import { RobotDetailPanel } from './robot-detail-panel';

const DETAIL = '/api/admin/analytics/robots/googlebot';
const ROUTE = { path: 'admin/robots/:key', component: RobotDetailPanel };

describe('RobotDetailPanel', () => {
  it('shows who the robot is, how it is checked, its days, paths, agents and hits', async () => {
    const { page, calls } = await openPanel(ROUTE, '/admin/robots/googlebot?range=90d', [
      { path: DETAIL, body: robotDetail() },
    ]);

    expect(calls[0]?.request.params.get('range')).toBe('90d');
    expect(page.querySelector('h1')?.textContent).toContain('Googlebot');
    expect(page.textContent).toContain('googlebot.com, google.com, googleusercontent.com');
    // 2xx to 5xx, always four lines.
    expect(page.querySelectorAll('lodb-time-series-chart polyline.ts-line')).toHaveLength(4);
    expect(page.textContent).toContain('/fr/16.1.1/items');
    expect(page.textContent).toContain('Mozilla/5.0 (compatible; Googlebot/2.1)');
    expect(page.querySelectorAll('lodb-robot-hits-table tbody tr')).toHaveLength(1);
    const journal = [...page.querySelectorAll('a')].find((link) =>
      link.getAttribute('href')?.startsWith('/admin/robots/journal'),
    );
    expect(journal?.getAttribute('href')).toBe('/admin/robots/journal?robot=googlebot');
  });

  it('says a robot unseen over the period was not seen, rather than fail', async () => {
    const { page } = await openPanel(ROUTE, '/admin/robots/googlebot?range=7d', [
      { path: DETAIL, body: { code: 'robot-not-found' }, status: 404 },
    ]);

    expect(page.textContent).toContain('admin.robots.detail.unseen');
    expect(page.textContent).not.toContain('admin.state.unavailable');
  });
});
