/** A list call: its version and language, and the page asked for; neither for the whole list. */
export interface ListRequest {
  readonly version: string;
  readonly lang: string;
  readonly page?: number;
  readonly size?: number;
}
