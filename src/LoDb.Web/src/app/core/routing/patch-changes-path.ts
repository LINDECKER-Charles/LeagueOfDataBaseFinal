/**
 * The page of what a patch changed, below the locale and the version (ADR 0005, amendment of
 * 2026-10-22): `/{locale}/changes` for the latest patch, `/{locale}/{version}/changes` for a
 * past one. Neither a resource nor shaped like a version, it never meets another page.
 */
export const PATCH_CHANGES_PATH = 'changes';
