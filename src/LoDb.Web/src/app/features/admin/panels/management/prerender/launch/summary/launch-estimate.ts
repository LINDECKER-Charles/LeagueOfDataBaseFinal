import type { PrerenderEstimateBasis } from '../../../../../../../core/api/generated/models/prerender-estimate-basis';
import type { PrerenderPatchRow } from '../../../../../../../core/api/generated/models/prerender-patch-row';
import type { PrerenderPatchesReport } from '../../../../../../../core/api/generated/models/prerender-patches-report';
import { RESOURCE_TYPE } from '../../../../../../../core/api/generated/models/resource-type-array';
import { isHeld } from '../../common/patches/is-held';
import type { LaunchDraft } from '../dialog/launch-draft';
import { type PagesPerLocale, pagesPerLocaleOf } from './pages-per-locale-of';

/** A patch whose pages a unit lists are not measured, and what stood in for them. */
export interface PagesGuess extends PagesPerLocale {
  readonly version: string;
}

/** Every figure of the footer and of the summary (§ 3.5); estimated, but `units`. */
export interface LaunchEstimate {
  readonly versions: number;
  readonly locales: number;
  readonly types: number;
  /** The (version, locale) rendered. */
  readonly units: number;
  /** The ones left out, already held in the epoch. */
  readonly skipped: number;
  /** The ones held already but rendered again, `skipComplete` turned off. */
  readonly rerendered: number;
  /** The share of a version's pages the types chosen make, 0 to 1. */
  readonly share: number;
  readonly pages: number;
  readonly bytesPerPage: number;
  readonly bytes: number;
  /** Pages a minute the duration assumes. */
  readonly pace: number;
  /** Whether the pace was measured, is the ceiling for want of a measure, or caps one. */
  readonly paceSource: 'measured' | 'ceiling' | 'capped';
  readonly seconds: number;
  /** Bytes of the units with no page stored yet: what the epoch grows by. */
  readonly addedBytes: number;
  readonly addedCost: number;
  /** The versions ticked measured themselves. */
  readonly measured: number;
  /** The versions ticked that were not, and what stood in for each. */
  readonly guesses: readonly PagesGuess[];
  /** Whether no older patch at all has a measure: the estimate is rough. */
  readonly isDefault: boolean;
}

// « 39 Ko en gzip », measured over 41,554 pages of prod: until the epoch has its own mean.
const DEFAULT_BYTES_PER_PAGE = 39_000;
const BYTES_PER_GB = 1e9;
const SECONDS_PER_MINUTE = 60;

interface Tally {
  units: number;
  rerendered: number;
  pages: number;
  /** Pages the epoch does not store yet, already weighed by the share of their types. */
  addedPages: number;
}

function shareOf(types: readonly string[], basis: PrerenderEstimateBasis): number {
  if (types.length === RESOURCE_TYPE.length) {
    return 1;
  }
  const shares = basis.typeShares;
  if (!shares) {
    return types.length / RESOURCE_TYPE.length;
  }
  return types.reduce((sum, type) => sum + (shares[type as keyof typeof shares] ?? 0), 0);
}

// A new epoch holds nothing, whatever the coverage read for it says.
function isHeldIn(draft: LaunchDraft, row: PrerenderPatchRow, at: number): boolean {
  const mask = row.types[at];
  return draft.epochKind !== 'new' && mask !== undefined && isHeld(mask, draft.pageTypes);
}

// The share of a version's pages that rendering a (version, locale) adds to the epoch: the
// types chosen its cell does not hold yet, by its `mask`. A cell is read alone: a failed or
// partial one adds its missing types even when other locales of the patch store pages.
function addedShare(
  draft: LaunchDraft,
  mask: string | undefined,
  basis: PrerenderEstimateBasis,
): number {
  if (draft.epochKind === 'new' || mask === undefined) {
    return shareOf(draft.pageTypes, basis);
  }
  const missing = draft.pageTypes.filter((type) => !isHeld(mask, [type]));
  return missing.length === 0 ? 0 : shareOf(missing, basis);
}

function tallyRow(draft: LaunchDraft, row: PrerenderPatchRow, report: PrerenderPatchesReport) {
  const doesSkip = draft.skipComplete && draft.epochKind !== 'new';
  const tally: Tally = { units: 0, rerendered: 0, pages: 0, addedPages: 0 };
  const perUnit = pagesPerLocaleOf(row.version, report.patches).pages;
  for (const locale of draft.locales) {
    const at = report.locales.indexOf(locale);
    const isHeldAlready = isHeldIn(draft, row, at);
    if (doesSkip && isHeldAlready) {
      continue;
    }
    tally.units += 1;
    tally.rerendered += isHeldAlready ? 1 : 0;
    tally.pages += perUnit;
    tally.addedPages += perUnit * addedShare(draft, row.types[at], report.estimates);
  }
  return tally;
}

function tallyOf(
  draft: LaunchDraft,
  rows: readonly PrerenderPatchRow[],
  report: PrerenderPatchesReport,
) {
  const total: Tally = { units: 0, rerendered: 0, pages: 0, addedPages: 0 };
  for (const row of rows) {
    const tally = tallyRow(draft, row, report);
    total.units += tally.units;
    total.rerendered += tally.rerendered;
    total.pages += tally.pages;
    total.addedPages += tally.addedPages;
  }
  return total;
}

function paceOf(basis: PrerenderEstimateBasis): Pick<LaunchEstimate, 'pace' | 'paceSource'> {
  const ceiling = basis.configuredRendersPerMinute;
  const measured = basis.measuredPagesPerMinute;
  if (measured == null) {
    return { pace: ceiling, paceSource: 'ceiling' };
  }
  return measured > ceiling
    ? { pace: ceiling, paceSource: 'capped' }
    : { pace: measured, paceSource: 'measured' };
}

function guessesOf(rows: readonly PrerenderPatchRow[], report: PrerenderPatchesReport) {
  return rows
    .filter((row) => row.pagesPerLocale == null)
    .map((row) => ({ version: row.version, ...pagesPerLocaleOf(row.version, report.patches) }));
}

/**
 * The figures of a launch (§ 3.5), from the draft and the coverage of the epoch it writes:
 * units (a locale already holding every type chosen is left out when `skipComplete` asks),
 * pages (each unit at its patch's pages a locale, times the share of the types), volume,
 * duration at the pace measured but never above the configured ceiling, and the cost of the
 * pages not stored yet, cell by cell, by the types each holds: rendering a stored page again
 * replaces it.
 */
export function launchEstimate(draft: LaunchDraft, report: PrerenderPatchesReport): LaunchEstimate {
  const rows = report.patches.filter((row) => draft.selection.selected.has(row.version));
  const { estimates } = report;
  const share = shareOf(draft.pageTypes, estimates);
  const tally = tallyOf(draft, rows, report);
  const bytesPerPage = estimates.bytesPerPage ?? DEFAULT_BYTES_PER_PAGE;
  const pages = Math.round(tally.pages * share);
  const addedBytes = Math.round(tally.addedPages) * bytesPerPage;
  const pace = paceOf(estimates);
  const guesses = guessesOf(rows, report);
  return {
    versions: rows.length,
    locales: draft.locales.length,
    types: draft.pageTypes.length,
    units: tally.units,
    skipped: rows.length * draft.locales.length - tally.units,
    rerendered: tally.rerendered,
    share,
    pages,
    bytesPerPage,
    bytes: pages * bytesPerPage,
    ...pace,
    seconds: pace.pace > 0 ? (pages / pace.pace) * SECONDS_PER_MINUTE : 0,
    addedBytes,
    addedCost: (addedBytes / BYTES_PER_GB) * estimates.pricePerGbMonthOneZone,
    measured: rows.length - guesses.length,
    guesses,
    isDefault: report.patches.every((row) => row.pagesPerLocale == null),
  };
}
