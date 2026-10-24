import { robotReport } from '../../../../testing/fixtures/robot-report';
import { openPanel } from '../../../../testing/panels/open-panel';
import { RobotsPanel } from './robots-panel';

const ROBOTS = '/api/admin/analytics/robots';

function texts(root: ParentNode, selector: string): string[] {
  return [...root.querySelectorAll(selector)].map((node) =>
    (node.textContent ?? '').replace(/\s+/g, ' ').trim(),
  );
}

describe('RobotsPanel', () => {
  it('counts the robots of the period, charts them by category and lists each one', async () => {
    const { page, calls } = await openPanel(RobotsPanel, '/admin/robots?range=7d', [
      { path: ROBOTS, body: robotReport() },
    ]);

    expect(calls[0]?.request.params.get('range')).toBe('7d');
    expect(texts(page, 'lodb-kpi .font-beaufort')).toEqual([
      '4k',
      '2',
      '72.5 %',
      '100',
      '200',
      '330 ms',
    ]);
    // A line per category, as the report lists them.
    expect(page.querySelectorAll('lodb-time-series-chart polyline.ts-line')).toHaveLength(2);
    const rows = page.querySelectorAll('lodb-robots-table tbody tr');
    expect(rows).toHaveLength(2);
    expect(rows[0]?.querySelector('a')?.getAttribute('href')).toBe(
      '/admin/robots/googlebot?range=7d',
    );
    expect(rows[1]?.textContent).toContain('admin.robots.undeclared_name');
    expect(texts(page, 'tbody td.font-mono')).toContain('googlebot.com');
  });

  it('filters its table by category and by a text, on the page itself', async () => {
    const { page, harness } = await openPanel(RobotsPanel, '/admin/robots', [
      { path: ROBOTS, body: robotReport() },
    ]);
    const table = page.querySelector('lodb-robots-table') as HTMLElement;
    const category = table.querySelector<HTMLSelectElement>('select');

    category!.value = 'undeclared';
    category!.dispatchEvent(new Event('change'));
    harness.detectChanges();
    expect(table.querySelectorAll('tbody tr a')).toHaveLength(1);

    category!.value = '';
    category!.dispatchEvent(new Event('change'));
    const search = table.querySelector<HTMLInputElement>('input[type="search"]');
    search!.value = 'GOOGLE';
    search!.dispatchEvent(new Event('input'));
    harness.detectChanges();
    expect(texts(table, 'tbody tr a')).toEqual(['Googlebot']);
  });
});
