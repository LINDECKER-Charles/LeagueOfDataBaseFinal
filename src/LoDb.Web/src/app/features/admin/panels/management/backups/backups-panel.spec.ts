import { backupReport } from '../../../testing/fixtures/backup-report';
import { openPanel } from '../../../testing/panels/open-panel';
import { openPanelInFrench } from '../../../testing/panels/open-panel-in-french';
import { BackupsPanel } from './backups-panel';

const BACKUPS = '/api/admin/backups';

function texts(root: ParentNode, selector: string): string[] {
  return [...root.querySelectorAll(selector)].map((node) =>
    (node.textContent ?? '').replace(/\s+/g, ' ').trim(),
  );
}

describe('BackupsPanel', () => {
  it('asks for the window of the URL', async () => {
    const { calls } = await openPanel(BackupsPanel, '/admin/backups?days=7', [
      { path: BACKUPS, body: backupReport() },
    ]);

    expect(calls[0].request.params.get('days')).toBe('7');
  });

  it('shows the key figures, a card per database, the nights, then every step', async () => {
    const { page } = await openPanel(BackupsPanel, '/admin/backups?days=7', [
      { path: BACKUPS, body: backupReport() },
    ]);

    expect(texts(page, 'lodb-kpi')).toEqual([
      'admin.backups.kpi.databases 1 / 2 admin.backups.kpi.databases_sub',
      'admin.backups.kpi.last_success 12/10/2026 02:01 admin.backups.kpi.last_success_sub',
      'admin.backups.kpi.repository 3.00 GB admin.backups.kpi.repository_sub',
      'admin.backups.kpi.cost 0,05 € admin.backups.kpi.cost_sub',
    ]);
    expect(page.querySelector('[data-database="umami"] span.inline-flex')?.className).toContain(
      'text-danger-light',
    );
    expect(texts(page, '[data-database="lodb"] [lodbChip]')).toEqual([
      'admin.backups.card.last_success',
      'admin.backups.card.dump',
    ]);
    expect(page.querySelectorAll('lodb-backup-nights-strip ol li')).toHaveLength(7);
    expect(page.querySelector('[data-night="2026-10-12"]')?.getAttribute('data-outcome')).toBe(
      'failed',
    );
    expect(page.querySelector('[data-night="2026-10-11"]')?.getAttribute('data-outcome')).toBe(
      'ok',
    );
    expect(texts(page, 'tbody tr[data-run]')).toHaveLength(5);
    expect(texts(page, 'tr[data-run="4"] td')).toEqual([
      '12/10/2026 03:01',
      'admin.backups.steps.backup',
      'umami',
      'admin.backups.results.failed',
      '1 min',
      '—',
      'connection to server at "analytics-postgres" failed',
    ]);
    expect(texts(page, 'tr[data-run="5"] td')[2]).toBe('admin.backups.repository');
  });

  it('folds a long history under its latest steps', async () => {
    const [latest] = backupReport().runs;
    const runs = Array.from({ length: 20 }, (_, index) => ({ ...latest, id: 100 - index }));
    const { page } = await openPanel(BackupsPanel, '/admin/backups', [
      { path: BACKUPS, body: backupReport({ runs }) },
    ]);

    expect(page.querySelectorAll('tbody tr[data-run]')).toHaveLength(15);
    expect(texts(page, 'lodb-fold-toggle button')).toEqual(['admin.rank.more']);
  });

  it('says the backups are not active yet when the journal is empty', async () => {
    const { page, calls } = await openPanel(BackupsPanel, '/admin/backups?days=365', [
      { path: BACKUPS, body: backupReport({ databases: [], repository: null, runs: [] }) },
    ]);

    expect(calls[0].request.params.get('days')).toBe('30');
    expect(texts(page, '[role="alert"]')).toEqual(['admin.backups.empty']);
    expect(page.querySelector('lodb-backup-database-card')).toBeNull();
    expect(texts(page, 'lodb-kpi')[0]).toBe(
      'admin.backups.kpi.databases 0 / 0 admin.backups.kpi.databases_none',
    );
    expect(texts(page, 'lodb-kpi')[3]).toBe(
      'admin.backups.kpi.cost — admin.backups.kpi.repository_none',
    );
    expect(texts(page, '.t-empty')).toEqual(['admin.backups.runs_empty']);
  });

  it('reads in French, the databases by their names', async () => {
    const { page } = await openPanelInFrench(BackupsPanel, '/admin/backups?days=7', [
      { path: BACKUPS, body: backupReport() },
    ]);

    expect(texts(page, 'lodb-backup-database-card h2')).toEqual([
      'LoDb (production)',
      'Umami (audience)',
    ]);
    expect(texts(page, 'lodb-backup-database-card h2 + span')).toEqual(['à jour', 'en échec']);
  });
});
