import type { PrerenderReport } from '../../../../../../../core/api/generated/models/prerender-report';

/** What the target epoch holds, which the switch dialog says first. */
export type SwitchState =
  | {
      readonly kind: 'ready';
      readonly versions: number;
      readonly failedUnits: number;
      readonly failedPages: number;
    }
  | { readonly kind: 'partial'; readonly share: number; readonly missing: number };

/** The variables of the retirement of an epoch no longer served. */
export interface RetireSnippet {
  /** The variables to add to the command of tools/prerender/README.md, § 4. */
  readonly line: string;
  /** Whether the line is whole: without the bucket's name it keeps a placeholder. */
  readonly canCopy: boolean;
  /** What the epoch costs a month until its lifecycle deletes it. */
  readonly monthlyCost: number;
}

/** The texts of the switch to an epoch, or of the retirement of one. */
export interface SwitchSnippets {
  readonly target: number;
  readonly served: number;
  /** The variable that serves the target, once set and redeployed. */
  readonly variable: string;
  /** Null when the target's coverage is unknown. */
  readonly state: SwitchState | null;
  /** Whether the active pass still writes the target. */
  readonly isWritten: boolean;
  /** Only for an epoch older than the served one: before the switch, it is text alone. */
  readonly retire: RetireSnippet | null;
}

// The names of the repository (configuration.md § 4.8, tools/prerender/README.md § 4):
// neither the environments nor their variables change names.
const SERVE_VARIABLE = 'LODB_ARCHIVE_SERVE_EPOCH';
const NO_BUCKET = '<bucket>';
const STAGING = 'staging';
const PRODUCTION = 'prod';

function stateOf(report: PrerenderReport, target: number): SwitchState | null {
  const coverage = report.storage.epochs.find((epoch) => epoch.epoch === target)?.coverage;
  if (!coverage || coverage.expectedUnits === 0) {
    return null;
  }
  if (coverage.gapUnits === 0) {
    const { pinnedVersions: versions, failedUnits, failedPages } = coverage;
    return { kind: 'ready', versions, failedUnits, failedPages };
  }
  return {
    kind: 'partial',
    share: coverage.completeUnits / coverage.expectedUnits,
    missing: coverage.expectedUnits - coverage.completeUnits,
  };
}

// The retirement is safe only once the epoch is no longer served: the script compares the
// epoch to retire with the served and written ones it is given, nothing else.
function retireOf(report: PrerenderReport, target: number): RetireSnippet {
  const bucket = report.storeBucket ?? null;
  // configure.sh requires the bucket to end with its environment: lodb-pages-prod.
  const environment = bucket?.endsWith(`-${STAGING}`) ? STAGING : PRODUCTION;
  const served = report.servedEpoch;
  const written = report.current?.epoch ?? served;
  const line = [
    `ENVIRONMENT=${environment}`,
    `BUCKET=${bucket ?? NO_BUCKET}`,
    `SERVED_EPOCH=${served}`,
    `WRITE_EPOCH=${written}`,
    `RETIRED_EPOCHS=${target}`,
  ].join(' ');
  const volume = report.storage.epochs.find((epoch) => epoch.epoch === target);
  return { line, canCopy: bucket !== null, monthlyCost: volume?.monthlyCost ?? 0 };
}

/**
 * The texts of the switch dialog (§ 5.5) for `target`: the variable to set, what the epoch
 * holds, whether a pass still writes it; and, for an epoch already retired (older than the
 * served one), the exact variables of its retirement, which are only safe then.
 */
export function switchSnippets(target: number, report: PrerenderReport): SwitchSnippets {
  return {
    target,
    served: report.servedEpoch,
    variable: `${SERVE_VARIABLE}=${target}`,
    state: stateOf(report, target),
    isWritten: report.current?.epoch === target,
    retire: target < report.servedEpoch ? retireOf(report, target) : null,
  };
}
