/** Chip tones and their utility, spelled out whole for the Tailwind scanner. */
export const CHIP_TONES = {
  /** Muted mono label: counts, tags, metadata. */
  muted: 'hx-chip',
  /** Cyan "live" label: HTTP verbs, active plan, current badge. */
  live: 'hx-chip-hex',
} as const;

export type ChipTone = keyof typeof CHIP_TONES;
