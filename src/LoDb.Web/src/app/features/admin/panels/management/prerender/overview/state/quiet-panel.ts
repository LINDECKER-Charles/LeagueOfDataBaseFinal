import { computed } from '@angular/core';
import type { Panel } from '../../../../../state/panel';

const SESSION = 'session';

/**
 * The page's report as `PanelState` should see it (§ 2.4): a failure shows a band only when
 * nothing is on screen yet, or when the session is gone. A failed refresh keeps the report
 * and says nothing here: the freshness of the head turns late, then lost, and a redeploy in
 * the middle of a pass of several days raises no false alarm.
 */
export function quietPanel<T>(panel: Panel<T>): Panel<T> {
  return {
    value: panel.value,
    busy: panel.busy,
    reload: () => panel.reload(),
    failure: computed(() => {
      const failure = panel.failure();
      if (failure === null || failure.kind === SESSION) {
        return failure;
      }
      return panel.value() === undefined ? failure : null;
    }),
  };
}
