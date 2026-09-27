import type { AdminBuildPage } from '../../../../../core/api/generated/models/admin-build-page';
import type { AdminBuildRow } from '../../../../../core/api/generated/models/admin-build-row';
import type { AdminVisit } from '../../../testing/admin-visit';
import { openPanelInFrench } from '../../../testing/panels/open-panel-in-french';
import { openPanel } from '../../../testing/panels/open-panel';
import { press } from '../../../testing/dom/press';
import { reply } from '../../../testing/http/reply';
import { sent } from '../../../testing/http/sent';
import { submit } from '../../../testing/dom/submit';
import { notice } from '../../../testing/dom/notice';
import { BuildsPanel } from './builds-panel';

const BUILDS = '/api/admin/builds';

function build(id: number, isPublic: boolean): AdminBuildRow {
  return {
    id,
    name: `Build ${id}`,
    championId: 'Ahri',
    gameMode: 'CLASSIC',
    gameVersion: '16.19.1',
    language: 'fr_FR',
    isPublic,
    score: id === 1 ? 12 : 0,
    createdAt: '2026-09-20T10:00:00Z',
    owner: { id: 9, username: 'faker', isBanned: false },
    shareToken: String(id).padStart(24, '0'),
  };
}

function buildsPage(items = [build(1, true), build(2, false)]): AdminBuildPage {
  return { items, page: 1, pages: 2, total: 30, stats: { total: 30, public: 12 } };
}

function row(visit: AdminVisit, id: number): Element {
  const found = visit.page.querySelector(`[data-build="${id}"]`);
  if (!found) {
    throw new Error(`No row for build ${id}.`);
  }
  return found;
}

function open(url = '/admin/builds') {
  return openPanel(BuildsPanel, url, [{ path: BUILDS, body: buildsPage() }]);
}

describe('BuildsPanel', () => {
  it('lists the builds with their mode, author, score and visibility', async () => {
    const visit = await open();

    const kpis = [...visit.page.querySelectorAll('lodb-kpi')].map((kpi) => kpi.textContent);
    expect(kpis).toEqual([expect.stringContaining('30'), expect.stringContaining('12')]);
    const cells = [...row(visit, 1).querySelectorAll('td')].map((cell) =>
      (cell.textContent ?? '').replace(/\s+/g, ' ').trim(),
    );
    expect(cells.slice(0, 5)).toEqual([
      'Build 1 Ahri',
      'CLASSIC16.19.1',
      'faker',
      '+12',
      'admin.builds.visibilities.public',
    ]);
    expect(row(visit, 2).querySelectorAll('td')[3]?.textContent?.trim()).toBe('0');
    const view = row(visit, 1).querySelector('a');
    expect(view?.getAttribute('href')).toBe(`/b/${'1'.padStart(24, '0')}`);
    expect(view?.getAttribute('target')).toBe('_blank');
    expect(row(visit, 2).textContent).toContain('admin.builds.visibilities.private');
    expect(row(visit, 1).textContent).toContain('admin.builds.unpublish');
    expect(row(visit, 2).textContent).not.toContain('admin.builds.unpublish');
    expect(visit.page.querySelector('lodb-admin-pager a')?.getAttribute('href')).toBe(
      '/admin/builds?page=2',
    );
  });

  it('filters by name and visibility through the URL', async () => {
    const visit = await open();

    submit(visit, { q: 'ahri mid', visibility: 'public' });
    const search = await sent(visit, BUILDS);
    await reply(visit, search, buildsPage([build(1, true)]));

    expect(search.request.params.get('q')).toBe('ahri mid');
    expect(search.request.params.get('visibility')).toBe('public');
    expect(visit.page.querySelector<HTMLSelectElement>('select')?.value).toBe('public');
  });

  it('shows the ban of an author next to its name', async () => {
    const banned = { ...build(1, true), owner: { id: 9, username: 'faker', isBanned: true } };
    const visit = await openPanel(BuildsPanel, '/admin/builds', [
      { path: BUILDS, body: buildsPage([banned]) },
    ]);

    expect(row(visit, 1).querySelectorAll('td')[2]?.textContent).toContain(
      'admin.users.badges.banned',
    );
  });

  it('takes a public build off the public pages', async () => {
    const visit = await open();

    press(visit, 'admin.builds.unpublish', row(visit, 1));
    (await sent(visit, `${BUILDS}/1/unpublish`, 'POST')).flush('');
    await reply(visit, await sent(visit, BUILDS), buildsPage([build(1, false), build(2, false)]));

    expect(notice()).toBe('notice: admin.builds.done.unpublish');
    expect(row(visit, 1).textContent).toContain('admin.builds.visibilities.private');
  });

  it('deletes a build once confirmed', async () => {
    const visit = await open();

    press(visit, 'admin.actions.delete', row(visit, 2));
    press(visit, 'admin.builds.confirm_delete', row(visit, 2));
    (await sent(visit, `${BUILDS}/2`, 'DELETE')).flush('');
    await reply(visit, await sent(visit, BUILDS), buildsPage([build(1, true)]));

    expect(notice()).toBe('notice: admin.builds.done.delete');
    expect(visit.page.querySelector('[data-build="2"]')).toBeNull();
  });

  it('keeps the visibility in French once unpublished, the site in English', async () => {
    const visit = await openPanelInFrench(BuildsPanel, '/admin/builds', [
      { path: BUILDS, body: buildsPage() },
    ]);
    expect(row(visit, 1).textContent).toContain('public');

    press(visit, 'Dépublier', row(visit, 1));
    (await sent(visit, `${BUILDS}/1/unpublish`, 'POST')).flush('');
    await reply(visit, await sent(visit, BUILDS), buildsPage([build(1, false), build(2, false)]));

    expect(row(visit, 1).textContent).toContain('privé');
    expect(visit.page.querySelector('tbody')?.textContent).not.toContain('private');
  });
});
