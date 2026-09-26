/**
 * Button tones and the utility each one applies. The class names are spelled out whole
 * because Tailwind only emits a `@utility` whose name it finds in a scanned file.
 */
export const BUTTON_TONES = {
  /** Gold outline on a gold wash: the default action. */
  primary: 'hx-btn',
  /** Muted outline that lights cyan: secondary and cancel actions. */
  ghost: 'hx-btn-ghost',
  /** Filled parchment-to-gold face: the one strongest call to action of a view. */
  gold: 'hx-btn-gold',
} as const;

export type ButtonTone = keyof typeof BUTTON_TONES;
