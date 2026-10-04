/**
 * Button sizes and the utility each one adds to the tone. Spelled out whole, like the tones,
 * for Tailwind's scanner.
 */
export const BUTTON_SIZES = {
  /** The tone's own size: 42px, 44px on a coarse pointer. */
  default: '',
  /** Compact type and padding: the actions of a table row or a toolbar. */
  small: 'hx-btn-sm',
} as const;

export type ButtonSize = keyof typeof BUTTON_SIZES;
