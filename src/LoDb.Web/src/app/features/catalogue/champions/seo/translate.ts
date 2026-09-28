/** Translates a key of the loaded catalogues into the page's locale. */
export type Translate = (key: string, params?: Readonly<Record<string, unknown>>) => string;
