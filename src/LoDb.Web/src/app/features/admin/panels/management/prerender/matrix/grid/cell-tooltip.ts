import type { PrerenderUnitView } from '../../../../../../../core/api/generated/models/prerender-unit-view';
import { duration } from '../../../../../format/duration';
import { figure } from '../../../../../format/figure';
import type { AdminText } from '../../../../../shared/inject-admin-text';
import { localeName } from '../../common/patches/locale-name';
import { decimalBytes } from '../../format/decimal-bytes';
import { type CellMode, cellStateOf } from './cell-state';

/** What the tooltip of a cell knows of it. */
export interface CellTooltipInput {
  readonly mode: CellMode;
  readonly version: string;
  readonly locale: string;
  /** Its code, from the cells of its version. */
  readonly code: string;
  /** The unit, when the client knows it in detail (running, recent, failures). */
  readonly unit?: PrerenderUnitView;
  /** Where the worker reaches its version, for a cell to come. */
  readonly rank?: number;
  /** In a coverage, the hexadecimal mask of the types it holds. */
  readonly held?: string;
}

const MATRIX = 'prerender.common.matrix';
const SEPARATOR = ' · ';
const HEXADECIMAL = 16;
// The bits of a mask of types held, in the order of the API's ResourceType.
const TYPES = ['champions', 'items', 'runes', 'summoners'] as const;
const MS_PER_SECOND = 1_000;

function heldOf(mask: string, texts: AdminText): string {
  const bits = Number.parseInt(mask, HEXADECIMAL) || 0;
  const types = TYPES.filter((_, bit) => (bits >> bit) & 1);
  if (types.length === 0) {
    return texts.text(`${MATRIX}.cell_held_none`);
  }
  const names = types.map((type) => texts.term('prerender.page_types', type));
  return texts.text(`${MATRIX}.cell_held`, { types: names.join(', ') });
}

function sinceOf(unit: PrerenderUnitView): number {
  const started = unit.startedAt ? Date.parse(unit.startedAt) : Number.NaN;
  return (Date.parse(unit.updatedAt) - started) / MS_PER_SECOND;
}

// What a unit known in detail adds: its pages, and how long it has run when it runs.
function unitLine(input: CellTooltipInput, unit: PrerenderUnitView, texts: AdminText): string {
  if (input.code === 'z') {
    return texts.text(`${MATRIX}.cell_stopped`, {
      done: figure(unit.cursor),
      total: figure(unit.pagesTotal),
    });
  }
  const processed = unit.pagesWritten + unit.pagesSkipped + unit.pagesFailed;
  const pages = texts.text(`${MATRIX}.cell_pages`, {
    done: figure(processed),
    total: unit.pagesTotal,
    size: decimalBytes(unit.bytesWritten),
  });
  const since = sinceOf(unit);
  if (input.code !== 'r' || Number.isNaN(since)) {
    return pages;
  }
  return pages + SEPARATOR + texts.text(`${MATRIX}.cell_since`, { duration: duration(since) });
}

/**
 * The tooltip of a cell of the matrix (§ 4.4), one delegated for the whole grid: the version,
 * the locale and its name, the state; then what the client knows of the unit (its pages and
 * volume, how long it runs, where it stopped), or the rank of a cell to come; in a coverage,
 * the types it holds. Lines split by a line break.
 */
export function cellTooltip(input: CellTooltipInput, texts: AdminText): string {
  const state = texts.text(`prerender.common.cells.${cellStateOf(input.code, input.mode)}`);
  const head = texts.text(`${MATRIX}.cell`, {
    version: input.version,
    locale: input.locale,
    language: localeName(input.locale),
    state,
  });
  if (input.mode === 'coverage') {
    return input.held === undefined ? head : head + SEPARATOR + heldOf(input.held, texts);
  }
  if (input.unit) {
    return `${head}\n${unitLine(input, input.unit, texts)}`;
  }
  if (input.code === '.' && input.rank !== undefined) {
    return `${head}\n${texts.text(`${MATRIX}.cell_rank`, { rank: figure(input.rank) })}`;
  }
  return head;
}
