/** The periods of the analytics report, those of the legacy admin. */
export const ADMIN_RANGES = ['7d', '30d', '90d', 'all'] as const;

export type AdminRange = (typeof ADMIN_RANGES)[number];
