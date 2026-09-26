/** What the routing of a request reads from it, as plain values a test can write. */
export interface RequestFacts {
  readonly method: string;
  readonly url: string;
  /** Request.mode: `navigate` for a document the browser loads. */
  readonly mode: string;
  /** The Accept header, empty when absent. */
  readonly accept: string;
  readonly hasRange: boolean;
}
