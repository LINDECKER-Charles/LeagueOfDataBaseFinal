import type { DialogSize } from './dialog-size';

/** What a caller says about a dialog it opens; the look and the behaviour are fixed here. */
export interface DialogOptions<D = unknown> {
  /** Id of the element naming the dialog, usually the heading of its lodb-dialog frame. */
  readonly labelledBy: string;
  /** Data handed to the dialog component through the CDK's DIALOG_DATA. */
  readonly data?: D;
  /** Width of the pane (dialog-size.ts); the theme picker's 46rem when left out. */
  readonly size?: DialogSize;
  /**
   * Selector of the control that takes the focus on opening, such as a picker's search;
   * the dialog itself when left out, which a screen reader then announces by name.
   */
  readonly autoFocus?: string;
}
