import type { ResourceType } from '../../api/generated/models/resource-type';
import type { WarmUpProgress } from '../../api/generated/models/warm-up-progress';

/**
 * The frame a run shows before the stream's first: the lists named, none ready, so the
 * modal is whole from its first paint.
 */
export function preparingFrame(resources: readonly ResourceType[]): WarmUpProgress {
  return {
    stage: 'preparing',
    total: 0,
    settled: 0,
    resources: resources.map((resource) => ({ resource, total: 0, settled: 0, ready: false })),
  };
}
