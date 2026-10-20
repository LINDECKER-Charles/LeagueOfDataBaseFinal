/** The hashed files of a build, as the pre-render pass copies them before any of its pages. */
export interface BuildListing {
  /** The build's identity: the digest of its sorted file names, each named by its content. */
  readonly build: string;
  /** Its hashed files, relative to the browser output (`main-ABCD1234.js`, `media/…`). */
  readonly files: readonly string[];
}
