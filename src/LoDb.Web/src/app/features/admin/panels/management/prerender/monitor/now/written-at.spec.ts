import { prerenderProgress } from '../../../../../testing/fixtures/prerender-progress';
import { writtenAtOf } from './written-at';

const BASE = prerenderProgress();
// The French unit wrote its last page at 08:42:03; the answer came at 08:42:05.
const LAST_PAGE = Date.parse('2026-10-21T08:42:03+00:00');

function answer(at: string, written: number, failed = BASE.pass.pagesFailed) {
  return prerenderProgress({
    generatedAt: at,
    pass: { ...BASE.pass, pagesWritten: written, pagesFailed: failed },
    running: [{ ...BASE.running[0], updatedAt: at }],
  });
}

describe('writtenAtOf', () => {
  it('starts from the last change the first answer dates', () => {
    expect(writtenAtOf(undefined, BASE)).toEqual({
      pages: BASE.pass.pagesWritten + BASE.pass.pagesSkipped,
      at: LAST_PAGE,
    });
  });

  it('moves to the answer that saw a page written', () => {
    const first = writtenAtOf(undefined, BASE);
    const next = answer('2026-10-21T08:42:10+00:00', BASE.pass.pagesWritten + 4);

    expect(writtenAtOf(first, next).at).toBe(Date.parse('2026-10-21T08:42:10+00:00'));
  });

  it('stays put while the pages fail, though each failure rewrites the unit', () => {
    const first = writtenAtOf(undefined, BASE);
    const failing = answer('2026-10-21T08:47:00+00:00', BASE.pass.pagesWritten, 400);

    expect(writtenAtOf(first, failing)).toBe(first);
  });
});
