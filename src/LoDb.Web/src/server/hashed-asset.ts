// With `outputHashing: all`, Angular writes bundles at the browser root as `<name>-<hash>.js|css`
// and media under media/ as `<name>-<hash>.<ext>`. A hash is eight characters of `[\w-]`, the
// alphabet Angular's chunk optimizer itself matches: esbuild emits upper-case base32
// (`main-2ZNNMVSG.js`), rolldown mixed case with `-` and `_` (`chunk-C1xKWD6m.js`).
const HASHED_BUNDLE = /^[^/]+-[\w-]{8}\.(?:js|css)$/;
const HASHED_MEDIA = /^media\/[^/]+-[\w-]{8}\.\w+$/;

/**
 * Whether a path relative to the browser output folder names a hashed build file. Files of
 * public/ keep their own name, so public/ holds no media/ folder and no root script or
 * stylesheet named like a bundle.
 */
export function isHashedAsset(relativePath: string): boolean {
  return HASHED_BUNDLE.test(relativePath) || HASHED_MEDIA.test(relativePath);
}
