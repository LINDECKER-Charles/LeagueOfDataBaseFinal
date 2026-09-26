/**
 * Frame styles and the utilities each one stacks, spelled out whole for the Tailwind
 * scanner. The frame clips its corners with the theme's bevel, so it carries no outline of
 * its own: focus is drawn by the controls inside.
 */
export const FRAME_STYLES = {
  /** Gold hairline, bevelled corners, soft drop: the default information panel. */
  plain: 'hextech-frame',
  /** Same panel, lit cyan on hover: a card that is a link. */
  interactive: 'hextech-frame hextech-frame-hover',
  /** Same panel with the etched corner brackets: a block that deserves weight. */
  ornate: 'hextech-frame hx-corners',
} as const;

export type FrameStyle = keyof typeof FRAME_STYLES;
