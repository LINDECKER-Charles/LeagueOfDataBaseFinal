/**
 * Names of the fields of a remembered context (`loc=<locale>&l=<lang>&v=<version>`), kept
 * short. `loc` is also read by nginx, which opens `/` on the remembered locale.
 */
export const PREFERENCES_FIELDS = { locale: 'loc', lang: 'l', version: 'v' } as const;
