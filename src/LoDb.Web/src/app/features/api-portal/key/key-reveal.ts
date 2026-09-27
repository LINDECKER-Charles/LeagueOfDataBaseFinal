import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  inject,
  input,
  signal,
} from '@angular/core';
import { TranslocoPipe } from '@jsverse/transloco';
import { Button } from '../../../ui/controls/button';
import { Field } from '../../../ui/controls/field';
import { Frame } from '../../../ui/surfaces/frame';

const COPIED_RESET_MS = 2000;

type CopyState = 'idle' | 'copied' | 'failed';

/**
 * The one showing of a secret just issued: a read-only field holding it and a copy button,
 * until the page is left, as in the legacy portal. Without the Clipboard API, or when it
 * refuses, the button says to copy by hand, and the field is selected on focus.
 */
@Component({
  selector: 'lodb-key-reveal',
  imports: [Button, Field, Frame, TranslocoPipe],
  template: `
    <section lodbFrame="ornate" class="key-reveal" aria-labelledby="api-raw-title">
      <h2 id="api-raw-title" class="portal-panel__title">
        {{ 'api.portal.raw.title' | transloco }}
      </h2>
      <p class="key-reveal__warning">{{ 'api.portal.raw.warning' | transloco }}</p>
      <div class="key-reveal__row">
        <label class="key-reveal__field">
          <span class="portal-label">{{ 'api.portal.raw.label' | transloco }}</span>
          <input
            #field
            lodbField
            type="text"
            readonly
            class="mt-1.5 w-full font-mono text-xs"
            data-testid="api-key-secret"
            [value]="secret()"
            (focus)="field.select()"
          />
        </label>
        <button type="button" lodbButton="ghost" aria-live="polite" (click)="copy()">
          {{ copyLabel() | transloco }}
        </button>
      </div>
    </section>
  `,
  styleUrls: ['../shared/portal.css', './key.css'],
  host: { class: 'block' },
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class KeyReveal {
  /** The secret, `lodb_` and 40 hexadecimal digits. */
  readonly secret = input.required<string>();

  private readonly state = signal<CopyState>('idle');
  private timer: ReturnType<typeof setTimeout> | undefined;

  constructor() {
    inject(DestroyRef).onDestroy(() => clearTimeout(this.timer));
  }

  protected copyLabel(): string {
    const state = this.state();
    if (state === 'copied') {
      return 'api.portal.raw.copied';
    }
    return state === 'failed' ? 'api.portal.raw.copy_error' : 'api.portal.raw.copy';
  }

  protected async copy(): Promise<void> {
    const clipboard = globalThis.navigator?.clipboard;
    try {
      if (!clipboard?.writeText) {
        throw new Error('No clipboard.');
      }
      await clipboard.writeText(this.secret());
      this.state.set('copied');
      clearTimeout(this.timer);
      this.timer = setTimeout(() => this.state.set('idle'), COPIED_RESET_MS);
    } catch {
      this.state.set('failed');
    }
  }
}
