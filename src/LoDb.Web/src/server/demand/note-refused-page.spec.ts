import { SHED_MARKER } from '../admission/shed-marker';
import { noteRefusedPage } from './note-refused-page';
import { RefusedPages } from './refused-pages';

const OLD_PAGE = '/fr/15.13.1/champions/Aatrox?tab=skins';

function counted(isPrerender: boolean, isShed: boolean, url = OLD_PAGE): number {
  const pages = new RefusedPages();
  const locals = isShed ? { [SHED_MARKER]: true } : {};
  noteRefusedPage(pages, { url, client: 'client', isPrerender }, { locals });
  return pages.size;
}

describe('noteRefusedPage', () => {
  it("counts a visitor's refused page of an older patch, normalised", () => {
    const pages = new RefusedPages();

    noteRefusedPage(
      pages,
      { url: OLD_PAGE, client: 'client', isPrerender: false },
      { locals: { [SHED_MARKER]: true } },
    );

    expect(pages.report(0).pages).toEqual([{ path: '/fr/15.13.1/champions/Aatrox', count: 1 }]);
  });

  it('ignores a page that rendered', () => {
    expect(counted(false, false)).toBe(0);
  });

  it("ignores the pass's own refusals", () => {
    expect(counted(true, true)).toBe(0);
  });

  it('ignores a refused page of the latest patch', () => {
    expect(counted(false, true, '/fr/champions/Aatrox')).toBe(0);
  });
});
