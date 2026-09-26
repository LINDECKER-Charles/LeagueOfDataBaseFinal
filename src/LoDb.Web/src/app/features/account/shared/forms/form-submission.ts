import { signal } from '@angular/core';
import type { FieldErrors } from '../problems/field-errors';
import { fieldErrorsOf } from '../problems/field-errors-of';
import { formErrorOf } from '../problems/form-error-of';
import { problemOf } from '../problems/problem-of';

/**
 * A form sent to the API: busy while on its way, then the message of what refused it, as a
 * whole or field by field.
 */
export class FormSubmission {
  readonly busy = signal(false);
  /** Translation key of what refused the whole form. */
  readonly error = signal<string | null>(null);
  /** Translation key of what refused each field. */
  readonly errors = signal<FieldErrors>({});

  /**
   * Sends the form and says whether it went through. `messages` names the form's own
   * refusals, by the API's code.
   */
  async send(
    call: () => Promise<unknown>,
    messages: Readonly<Record<string, string>> = {},
  ): Promise<boolean> {
    this.busy.set(true);
    this.error.set(null);
    this.errors.set({});
    try {
      await call();
      return true;
    } catch (error) {
      const problem = problemOf(error);
      this.errors.set(fieldErrorsOf(problem));
      this.error.set(messages[problem.code ?? ''] ?? formErrorOf(problem));
      return false;
    } finally {
      this.busy.set(false);
    }
  }
}
