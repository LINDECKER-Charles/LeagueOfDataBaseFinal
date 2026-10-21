import type { PrerenderPassProgress } from '../../../../../../../core/api/generated/models/prerender-pass-progress';

/** When the pass last wrote a page, as the monitor saw it. */
export interface WrittenAt {
  /** Pages written or skipped: what moves when the pass renders, not when its pages fail. */
  readonly pages: number;
  /** The server's instant it last grew, in milliseconds. */
  readonly at: number;
}

// Before any growth seen, the last change the answer dates: a unit's, or the pass's opening.
function firstOf(progress: PrerenderPassProgress): number {
  const stamps = [
    ...progress.running.map((unit) => unit.updatedAt),
    progress.recent[0]?.updatedAt,
    progress.pass.createdAt,
  ];
  return Math.max(...stamps.filter((at) => at !== undefined).map((at) => Date.parse(at)));
}

/**
 * When the pass last wrote a page (§ 4.5), answer after answer: the instant of the answer that
 * saw its pages written or skipped grow. A failed page rewrites its unit as a written one does,
 * so the stamps of the units cannot tell them apart: a unit failing each page would never look
 * stalled. Before the first growth seen, the last change the first answer dates.
 */
export function writtenAtOf(
  last: WrittenAt | undefined,
  progress: PrerenderPassProgress,
): WrittenAt {
  const { pagesWritten, pagesSkipped } = progress.pass;
  const pages = pagesWritten + pagesSkipped;
  if (!last) {
    return { pages, at: firstOf(progress) };
  }
  return pages > last.pages ? { pages, at: Date.parse(progress.generatedAt) } : last;
}
