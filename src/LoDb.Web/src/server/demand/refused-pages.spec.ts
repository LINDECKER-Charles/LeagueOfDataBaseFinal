import { RefusedPages } from './refused-pages';

const AATROX = '/fr/15.13.1/champions/Aatrox';
const ITEMS = '/fr/15.13.1/items';
const RUNES = '/en/14.1.1/runes';

describe('RefusedPages', () => {
  it('counts each page cumulatively, under an instance of its own', () => {
    const pages = new RefusedPages();

    pages.record(AATROX);
    pages.record(ITEMS);
    pages.record(AATROX);

    const report = pages.report(0);
    expect(report.instance).toMatch(/^[0-9a-f-]{36}$/);
    expect(report.pages).toEqual([
      { path: ITEMS, count: 1 },
      { path: AATROX, count: 2 },
    ]);
    expect(report).toMatchObject({ cursor: 3, isComplete: true, evicted: 0 });
    expect(new RefusedPages().instance).not.toBe(report.instance);
  });

  it('answers only the pages changed after the cursor, with their whole count', () => {
    const pages = new RefusedPages();
    pages.record(AATROX);
    pages.record(ITEMS);
    const { cursor } = pages.report(0);

    pages.record(AATROX);

    expect(pages.report(cursor).pages).toEqual([{ path: AATROX, count: 2 }]);
    expect(pages.report(pages.report(cursor).cursor).pages).toEqual([]);
  });

  it('keeps every reader on its own cursor: a read changes nothing', () => {
    const pages = new RefusedPages();
    pages.record(AATROX);

    expect(pages.report(0)).toEqual(pages.report(0));
  });

  it('cuts a long answer and resumes after its last change', () => {
    const pages = new RefusedPages();
    [AATROX, ITEMS, RUNES].forEach((path) => pages.record(path));

    const first = pages.report(0, 2);
    const rest = pages.report(first.cursor, 2);

    expect(first).toMatchObject({ cursor: 2, isComplete: false });
    expect(first.pages.map((page) => page.path)).toEqual([AATROX, ITEMS]);
    expect(rest).toMatchObject({ cursor: 3, isComplete: true });
    expect(rest.pages.map((page) => page.path)).toEqual([RUNES]);
  });

  it('drops the least refused tenth when full, the oldest first among equals', () => {
    const pages = new RefusedPages(10);
    for (let index = 0; index < 10; index += 1) {
      pages.record(`/fr/15.13.1/items/${index}`);
    }
    pages.record('/fr/15.13.1/items/0');

    pages.record(AATROX);

    const kept = pages.report(0).pages.map((page) => page.path);
    expect(pages.size).toBe(10);
    expect(kept).not.toContain('/fr/15.13.1/items/1');
    expect(kept).toContain('/fr/15.13.1/items/0');
    expect(kept).toContain(AATROX);
    expect(pages.report(0).evicted).toBe(1);
  });
});
