import { ChangeDetectionStrategy, Component, input, model, signal } from '@angular/core';
import { TranslocoPipe } from '@jsverse/transloco';
import { Field } from '../../../ui/controls/field';
import type { FieldErrors } from '../shared/problems/field-errors';
import { PasswordChecklist } from './password-checklist';

function valueOf(event: Event): string {
  return (event.target as HTMLInputElement).value;
}

/**
 * A new password and its confirmation, with the live checklist under them: registration,
 * reset and the first password of a Google account. The fields are named `password` and
 * `confirmation`; the errors the server gave are shown under each.
 */
@Component({
  selector: 'lodb-password-pair',
  imports: [Field, PasswordChecklist, TranslocoPipe],
  template: `<div>
      <label [for]="idPrefix() + '-password'" class="auth-label">
        {{ 'auth.register.password' | transloco }}
      </label>
      <input
        lodbField
        type="password"
        name="password"
        class="mt-1.5"
        required
        autocomplete="new-password"
        [id]="idPrefix() + '-password'"
        [value]="password()"
        [attr.aria-invalid]="errors()['password'] ? true : null"
        [attr.aria-describedby]="idPrefix() + '-checklist'"
        (input)="password.set(valueOf($event))"
        (blur)="touched.set(true)"
      />
      @if (errors()['password']; as error) {
        <p class="auth-field-error" role="alert">{{ error | transloco }}</p>
      }
    </div>
    <div>
      <label [for]="idPrefix() + '-confirmation'" class="auth-label">
        {{ 'auth.register.password_confirm' | transloco }}
      </label>
      <input
        lodbField
        type="password"
        name="confirmation"
        class="mt-1.5"
        required
        autocomplete="new-password"
        [id]="idPrefix() + '-confirmation'"
        [value]="confirmation()"
        [attr.aria-invalid]="errors()['confirmation'] ? true : null"
        (input)="confirmation.set(valueOf($event))"
      />
      @if (errors()['confirmation']; as error) {
        <p class="auth-field-error" role="alert">{{ error | transloco }}</p>
      }
    </div>
    <lodb-password-checklist
      [id]="idPrefix() + '-checklist'"
      [password]="password()"
      [confirmation]="confirmation()"
      [touched]="touched()"
    />`,
  styleUrl: '../auth/card/auth-form.css',
  host: { class: 'block space-y-5' },
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PasswordPair {
  /** Prefix of the fields' ids, unique in the page. */
  readonly idPrefix = input.required<string>();
  readonly errors = input<FieldErrors>({});
  readonly password = model('');
  readonly confirmation = model('');

  protected readonly touched = signal(false);
  protected readonly valueOf = valueOf;
}
