/** A translation of the page's locale, handed to the pure builders of its texts. */
export type Translate = (key: string, params?: Readonly<Record<string, unknown>>) => string;
