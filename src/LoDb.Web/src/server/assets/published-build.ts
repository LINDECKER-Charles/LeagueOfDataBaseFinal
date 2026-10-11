/** What `publishFrontBuilds` did with the hashed files of this image. */
export interface PublishedBuild {
  /** Files the shared folder lacked, copied in. */
  readonly copied: number;
  /** Files already there, from an earlier build or start, whose date was refreshed. */
  readonly refreshed: number;
}
