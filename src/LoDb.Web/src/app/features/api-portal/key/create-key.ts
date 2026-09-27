import { ChangeDetectionStrategy, Component, input, output, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { Button } from '../../../ui/controls/button';
import { Field } from '../../../ui/controls/field';
import { Frame } from '../../../ui/surfaces/frame';

/** The length of a key's name, as the API stores it. */
const NAME_MAX_LENGTH = 64;

/**
 * An account without a key: the form issuing one, or, before the e-mail is verified, the
 * reason it cannot and the way to verify it.
 */
@Component({
  selector: 'lodb-create-key',
  imports: [Button, Field, Frame, RouterLink, TranslocoPipe],
  template: `
    <section lodbFrame class="portal-panel" aria-labelledby="api-create-title">
      <h2 id="api-create-title" class="portal-panel__title">
        {{ 'api.portal.create.title' | transloco }}
      </h2>
      @if (verified()) {
        <p class="portal-text">{{ 'api.portal.create.body' | transloco }}</p>
        <form class="create-key__form" (submit)="submit($event)">
          <label class="create-key__name">
            <span class="portal-label">{{ 'api.portal.create.name_label' | transloco }}</span>
            <input
              lodbField
              type="text"
              name="name"
              autocomplete="off"
              class="mt-1.5"
              [maxLength]="maxLength"
              [placeholder]="'api.portal.create.name_placeholder' | transloco"
              [value]="name()"
              (input)="type($event)"
            />
          </label>
          <button lodbButton="gold" type="submit" [disabled]="busy()">
            {{ 'api.portal.create.submit' | transloco }}
          </button>
        </form>
      } @else {
        <p class="portal-text">{{ 'auth.verify.gate_api' | transloco }}</p>
        <a lodbButton class="mt-5" [routerLink]="verifyLink()">
          {{ 'apiPortal.create.verify' | transloco }}
        </a>
      }
    </section>
  `,
  styleUrls: ['../shared/portal.css', './key.css'],
  host: { class: 'block' },
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CreateKey {
  /** Whether the account's e-mail is verified, which issuing a key requires. */
  readonly verified = input.required<boolean>();
  readonly busy = input(false);
  /** Root-relative link of the verification page. */
  readonly verifyLink = input.required<string>();

  /** The name typed, possibly blank: the API names the key `default` then. */
  readonly create = output<string>();

  protected readonly maxLength = NAME_MAX_LENGTH;
  protected readonly name = signal('');

  protected type(event: Event): void {
    this.name.set((event.target as HTMLInputElement).value);
  }

  protected submit(event: Event): void {
    event.preventDefault();
    this.create.emit(this.name().trim());
  }
}
