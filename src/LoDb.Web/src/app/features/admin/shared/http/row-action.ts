import type { AdminCall } from './admin-call';
import type { AdminMessage } from './admin-message';

/** An action on a row of a list: the operation, its parameters and what its success says. */
export interface RowAction<P, T> {
  readonly call: AdminCall<P, T>;
  readonly params: P;
  readonly done: AdminMessage;
}
