/** One entry of the version list. */
export interface VersionOption {
  readonly version: string;
  /** Whether it is the latest version, the one the short URLs follow. */
  readonly latest: boolean;
}
