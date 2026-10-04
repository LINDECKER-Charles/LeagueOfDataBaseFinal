import type { Answer } from '../http/answer';

/** A call a panel sends when it opens, and what the API answers: a failure with `status`. */
export interface PanelCall {
  readonly path: string;
  readonly body: Answer;
  readonly status?: number;
}
