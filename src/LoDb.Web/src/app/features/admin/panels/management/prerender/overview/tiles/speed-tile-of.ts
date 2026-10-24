import type { PrerenderSpeedReport } from '../../../../../../../core/api/generated/models/prerender-speed-report';
import type { AdminText } from '../../../../../shared/inject-admin-text';
import type { StatusTile } from '../../../../../widgets/status-tabs/status-tile';

const SPEED = 'prerender.overview.views.speed';
const MANUAL = 'manual';
const HELD = 'held';
const NONE = '—';

// What follows the mode: the pace in force while a pass drains, else the target or the range.
function subOf(speed: PrerenderSpeedReport, texts: AdminText): string {
  const { pace, setting, limits } = speed;
  if (pace) {
    return texts.text(`${SPEED}.sub_now`, { pace: Math.round(pace.rendersPerMinute) });
  }
  return setting.mode === MANUAL
    ? texts.text(`${SPEED}.sub_manual`, { pace: setting.rendersPerMinute })
    : texts.text(`${SPEED}.sub_adaptive`, {
        min: limits.minRendersPerMinute,
        max: limits.maxRendersPerMinute,
      });
}

// Gold while the brake holds the pass, or while the manual target is past what the renderers
// sustain.
function toneOf({ pace, setting, limits }: PrerenderSpeedReport): StatusTile['tone'] {
  const isOver =
    setting.mode === MANUAL && setting.rendersPerMinute > limits.manualWarnRendersPerMinute;
  return pace?.state === HELD || isOver ? 'warn' : null;
}

/**
 * The tile of the speed: the mode in force (fixed when the environment cuts the regulator),
 * then the pace a draining pass runs at, the manual target or the adaptive range.
 */
export function speedTileOf(
  speed: PrerenderSpeedReport | undefined,
  texts: AdminText,
): Omit<StatusTile, 'key'> {
  const label = texts.text(`${SPEED}.label`);
  if (!speed) {
    return { label, value: NONE, sub: null, tone: null };
  }
  if (speed.isFixed) {
    return {
      label,
      value: texts.text(`${SPEED}.value_fixed`),
      sub: texts.text(`${SPEED}.sub_fixed`),
      tone: 'warn',
    };
  }
  const mode = speed.setting.mode === MANUAL ? 'manual' : 'adaptive';
  return {
    label,
    value: texts.text(`${SPEED}.value_${mode}`),
    sub: subOf(speed, texts),
    tone: toneOf(speed),
  };
}
