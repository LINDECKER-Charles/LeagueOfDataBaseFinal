/**
 * What the API said when it refused the app's version (`426 Upgrade Required`, L9.0). The
 * versions come from the answer's ProblemDetails; each is null when the answer lacked it.
 */
export interface UpdateRequirement {
  /** The version the app runs, as the API read it from `X-LoDb-Client`. */
  readonly clientVersion: string | null;
  /** The oldest version the API still serves. */
  readonly minimumVersion: string | null;
  /** The newest release, which the update brings. */
  readonly latestVersion: string | null;
}
