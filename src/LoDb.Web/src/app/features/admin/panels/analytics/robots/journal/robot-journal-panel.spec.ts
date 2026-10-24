import { robotHit, robotHitPage } from '../../../../testing/fixtures/robot-report';
import { openPanel } from '../../../../testing/panels/open-panel';
import { RobotJournalPanel } from './robot-journal-panel';

const HITS = '/api/admin/analytics/robot-hits';

describe('RobotJournalPanel', () => {
  it('reads its filters from the URL and lists the hits, the robot named', async () => {
    const { page, calls } = await openPanel(
      RobotJournalPanel,
      '/admin/robots/journal?robot=googlebot&status=5&q=items&page=2',
      [{ path: HITS, body: robotHitPage([robotHit()], true, 2) }],
    );

    const params = calls[0]!.request.params;
    expect(['robot', 'status', 'q', 'page'].map((name) => params.get(name))).toEqual([
      'googlebot',
      '5',
      'items',
      '2',
    ]);
    const cells = [...page.querySelectorAll('lodb-robot-hits-table tbody td')].map((cell) =>
      (cell.textContent ?? '').trim(),
    );
    expect(cells[1]).toContain('Googlebot');
    expect(cells[2]).toContain('admin.robots.verdicts.mismatch');
    expect(cells[3]).toBe('503');
    // A journal read page by page: the next one shows while the API says there is more.
    const previous = page.querySelector('lodb-admin-pager a');
    expect(previous!.getAttribute('href')).toContain('page=1');
  });

  it('keeps the selects of the filters on the values of the URL', async () => {
    const { page } = await openPanel(RobotJournalPanel, '/admin/robots/journal?verdict=mismatch', [
      { path: HITS, body: robotHitPage([]) },
    ]);

    const verdict = page.querySelector<HTMLSelectElement>('select[name="verdict"]');
    expect(verdict?.value).toBe('mismatch');
    expect(page.textContent).toContain('admin.robots.hits.empty');
  });
});
