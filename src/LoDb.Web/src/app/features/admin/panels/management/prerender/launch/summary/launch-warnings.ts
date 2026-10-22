import type { PrerenderReport } from '../../../../../../../core/api/generated/models/prerender-report';
import { RESOURCE_TYPE } from '../../../../../../../core/api/generated/models/resource-type-array';
import { duration } from '../../../../../format/duration';
import type { LaunchDraft } from '../dialog/launch-draft';
import type { LaunchEstimate } from './launch-estimate';

/** What the summary warns about before a launch: a key of `launcher.warnings` and its values. */
export interface LaunchWarning {
  readonly code:
    | 'rerender'
    | 'long'
    | 'types'
    | 'openers'
    | 'new_epoch'
    | 'standby_exists'
    | 'default_estimate'
    | 'priority_capped'
    | 'nothing';
  readonly params: Readonly<Record<string, string | number>>;
  /** Gold for a word of caution, bad for what forbids the launch. */
  readonly tone: 'gold' | 'bad';
}

// A pass longer than this outlives a few promotions: say so.
const LONG_SECONDS = 3 * 24 * 3_600;
const LONG_PAGES = 1_000_000;
const STANDBY = 'standby';

type Rule = (
  draft: LaunchDraft,
  estimate: LaunchEstimate,
  report: PrerenderReport,
) => LaunchWarning | null;

const gold = (code: LaunchWarning['code'], params: LaunchWarning['params'] = {}) =>
  ({ code, params, tone: 'gold' }) as const;

// In the order of § 3.5; `nothing`, which forbids the launch, comes first.
const RULES: readonly Rule[] = [
  (draft, estimate) =>
    estimate.units === 0 ? { code: 'nothing', params: { epoch: draft.epoch }, tone: 'bad' } : null,
  (draft, estimate) =>
    !draft.skipComplete && estimate.rerendered > 0
      ? gold('rerender', { units: estimate.rerendered })
      : null,
  (_, estimate) =>
    estimate.seconds > LONG_SECONDS || estimate.pages > LONG_PAGES
      ? gold('long', { duration: duration(estimate.seconds) })
      : null,
  (draft, _, report) =>
    draft.pageTypes.length < RESOURCE_TYPE.length &&
    report.enqueuesPromotions &&
    !draft.isOpenersFirst
      ? gold('types')
      : null,
  (draft) => (draft.isOpenersFirst ? gold('openers') : null),
  (draft) => (draft.epochKind === 'served' ? null : gold('new_epoch', { epoch: draft.epoch })),
  (draft, _, report) => {
    const standby = report.storage.epochs.find((volume) => volume.state === STANDBY);
    return draft.epochKind === 'new' && standby
      ? gold('standby_exists', { epoch: standby.epoch })
      : null;
  },
  (_, estimate) => (estimate.isDefault ? gold('default_estimate') : null),
  (draft) => (draft.isPriorityCapped ? gold('priority_capped') : null),
];

/** The warnings of a launch (§ 3.5): each one a word of caution, `nothing` a refusal. */
export function launchWarnings(
  draft: LaunchDraft,
  estimate: LaunchEstimate,
  report: PrerenderReport,
): LaunchWarning[] {
  return RULES.flatMap((rule) => rule(draft, estimate, report) ?? []);
}
