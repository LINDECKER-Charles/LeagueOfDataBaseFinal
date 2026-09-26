/** Skeleton shapes and the classes each one stacks, spelled out whole like the utilities. */
export const SKELETON_SHAPES = {
  /** One line of body text. */
  line: 'hx-sk hx-sk-line',
  /** A heading, narrower than the column. */
  title: 'hx-sk hx-sk-title',
  /** A control or a table row. */
  bar: 'hx-sk hx-sk-bar',
  /** A 16:9 media block. */
  block: 'hx-sk hx-sk-block',
  /** A grid tile. */
  tile: 'hx-sk hx-sk-tile',
  /** An avatar or an icon. */
  circle: 'hx-sk hx-sk-circle',
} as const;

export type SkeletonShape = keyof typeof SKELETON_SHAPES;
