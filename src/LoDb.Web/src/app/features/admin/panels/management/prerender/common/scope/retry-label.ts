import type { AdminText } from '../../../../../shared/inject-admin-text';
import type { FailuresPreset } from './failures-preset';

const RETRY = 'prerender.common.retry';

/**
 * The words of a relaunch of what a pass left undone (§ 3.1), which its units say: « Relancer
 * 4 unités » for failures, « Finir 4 unités non rendues » for what a cancelled pass never
 * rendered, « Relancer 2 en échec et 4 non rendues » for both; each with its ≈ pages.
 */
export function retryLabel(preset: FailuresPreset, texts: AdminText): string {
  const { units, failed, gaps, pages } = preset;
  if (gaps === 0) {
    return texts.text(`${RETRY}.units`, { units, pages });
  }
  return failed === 0
    ? texts.text(`${RETRY}.gaps`, { gaps, pages })
    : texts.text(`${RETRY}.units_gaps`, { failed, gaps, pages });
}
