/** A question asked before an action, and its consequences, translated. */
export interface ConfirmData {
  readonly title: string;
  readonly lines: readonly string[];
  readonly confirmLabel: string;
  /** The safe choice, focused on opening. */
  readonly keepLabel: string;
  readonly tone: 'danger' | 'gold';
}
