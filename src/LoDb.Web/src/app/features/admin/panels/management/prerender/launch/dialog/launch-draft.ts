import type { PrerenderPatchRow } from '../../../../../../../core/api/generated/models/prerender-patch-row';
import type { PrerenderPatchesReport } from '../../../../../../../core/api/generated/models/prerender-patches-report';
import type { PrerenderReport } from '../../../../../../../core/api/generated/models/prerender-report';
import { RESOURCE_TYPE } from '../../../../../../../core/api/generated/models/resource-type-array';
import { LOCALES } from '../../../../../../../core/i18n/locales';
import type { LaunchPreset } from '../../common/scope/launch-preset';
import type { PatchSelection } from '../../common/selection/patch-selection';
import { type EpochKind, epochChoices } from '../perimeter/epoch-choices';

/** What the launch dialog edits: the pass to launch, as the operator shapes it. */
export interface LaunchDraft {
  readonly origin: LaunchPreset['origin'];
  /** The patches ticked and the ones rendered first. */
  readonly selection: PatchSelection;
  /** The locales, in the site's order; all 21 when nothing restricts them. */
  readonly locales: readonly string[];
  /** The page types, in the API's order; all four when nothing restricts them. */
  readonly pageTypes: readonly string[];
  /** The epoch written, always explicit: a switch meanwhile must not move the target. */
  readonly epoch: number;
  readonly epochKind: EpochKind;
  readonly skipComplete: boolean;
  readonly fromPass: number | null;
  /**
   * Whether the ticks are read from the coverage yet: 'all', 'incomplete' and the lists wait
   * for it, to keep only the older patches it knows.
   */
  readonly isResolved: boolean;
  /** Whether the last paste had more than 100 versions, ticked without priority. */
  readonly isPriorityCapped: boolean;
}

// PassRequestReader.MaxPriorityVersions: the API refuses more.
const MAX_PRIORITY = 100;
// A gap of an epoch (§ 6.5): absent or partial. A failed cell is the relaunch's business.
const GAPS = '-p';

function kindOf(report: PrerenderReport, epoch: number): EpochKind {
  return epochChoices(report).find((choice) => choice.epoch === epoch)?.kind ?? 'new';
}

function ticked(preset: LaunchPreset, rows: readonly PrerenderPatchRow[]): string[] {
  const { versions } = preset;
  if (versions === 'all') {
    return rows.map((row) => row.version);
  }
  if (versions === 'incomplete') {
    const gaps = rows.filter((row) => [...row.cells].some((cell) => GAPS.includes(cell)));
    return gaps.map((row) => row.version);
  }
  const known = new Set(rows.map((row) => row.version));
  return versions.filter((version) => known.has(version));
}

function selectionOf(preset: LaunchPreset, patches: PrerenderPatchesReport | null) {
  const selected = new Set(patches ? ticked(preset, patches.patches) : []);
  const priority = preset.priority.filter((version) => selected.has(version));
  return { selected, anchor: null, priority: priority.slice(0, MAX_PRIORITY), extent: null };
}

/**
 * The draft a preset opens the dialog on (§ 3.1). Its ticks need the coverage of the epoch:
 * without `patches` none is ticked yet and the draft is not resolved; with them, 'all' ticks
 * every older patch, 'incomplete' each one with a gap (a cell - or p) and a list the older
 * patches it names. The priorities keep the ones ticked, 100 at most.
 */
export function draftOf(
  preset: LaunchPreset,
  report: PrerenderReport,
  patches: PrerenderPatchesReport | null,
): LaunchDraft {
  const epoch = preset.epoch ?? report.servedEpoch;
  return {
    origin: preset.origin,
    selection: selectionOf(preset, patches),
    locales: preset.locales ?? LOCALES,
    pageTypes: preset.pageTypes ?? RESOURCE_TYPE,
    epoch,
    epochKind: kindOf(report, epoch),
    skipComplete: preset.skipComplete,
    fromPass: preset.fromPass,
    isResolved: patches !== null,
    isPriorityCapped: false,
  };
}
