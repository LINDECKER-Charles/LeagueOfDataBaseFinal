import type { Segment } from '../widgets/segment-bar';
import { ADMIN_RANGES } from './admin-range';

/** The periods of an analytics panel as the segmented bar shows them: "30 j", named "30 jours". */
export const RANGE_SEGMENTS: readonly Segment[] = ADMIN_RANGES.map((range) => ({
  value: range,
  label: `admin.range.${range}`,
  name: `admin.range_long.${range}`,
}));
