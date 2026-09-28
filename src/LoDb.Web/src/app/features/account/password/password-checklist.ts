import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { TranslocoPipe } from '@jsverse/transloco';
import { type PasswordRule, passwordRules, passwordsMatch } from './password-rules';

/**
 * The rules of a new password, checked as it is typed, with the same rules as the server.
 * Before the field is left, the unmet rules stay muted: no red while one is still typing.
 * Each rule says whether it is met in words too, not in colour and shape alone.
 */
@Component({
  selector: 'lodb-password-checklist',
  imports: [TranslocoPipe],
  template: `<ul
    class="pwd-checklist"
    aria-live="polite"
    [class.pwd-checklist--touched]="touched()"
  >
    @for (rule of rules(); track rule.code) {
      <li class="pwd-checklist__item" [class.pwd-checklist__item--ok]="rule.met">
        <svg viewBox="0 0 12 12" width="11" height="11" aria-hidden="true">
          @if (rule.met) {
            <path
              d="M2 6.2 5 9l5-6"
              fill="none"
              stroke="currentColor"
              stroke-width="1.6"
              stroke-linecap="round"
              stroke-linejoin="round"
            />
          } @else {
            <path
              d="M6 1.5 10.5 6 6 10.5 1.5 6Z"
              fill="none"
              stroke="currentColor"
              stroke-width="1.2"
            />
          }
        </svg>
        {{ rule.code | transloco }}
        <span class="sr-only">
          {{ (rule.met ? 'account.password.met' : 'account.password.unmet') | transloco }}
        </span>
      </li>
    }
  </ul>`,
  styleUrl: './password-checklist.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PasswordChecklist {
  readonly password = input.required<string>();
  readonly confirmation = input.required<string>();
  /** Whether the password field was left once. */
  readonly touched = input(false);

  protected readonly rules = computed<PasswordRule[]>(() => [
    ...passwordRules(this.password()),
    {
      code: 'auth.password.rule_match',
      met: passwordsMatch(this.password(), this.confirmation()),
    },
  ]);
}
