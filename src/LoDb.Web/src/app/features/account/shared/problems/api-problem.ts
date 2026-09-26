/** What a refused API call says, read from its ProblemDetails. */
export interface ApiProblem {
  /** HTTP status; 0 when no answer came back at all. */
  readonly status: number;
  /** Stable kebab-case code, such as `invalid-credentials`; null when the answer had none. */
  readonly code: string | null;
  /** Codes of the invalid fields, by camelCase field name (`validation-failed`). */
  readonly errors: Readonly<Record<string, readonly string[]>>;
}
