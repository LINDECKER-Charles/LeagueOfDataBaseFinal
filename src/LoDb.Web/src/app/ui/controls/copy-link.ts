import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  type ElementRef,
  afterNextRender,
  booleanAttribute,
  inject,
  input,
  signal,
  viewChild,
  Injector,
} from '@angular/core';
import { Button } from './button';
import { Field } from './field';

const COPIED_RESET_MS = 2000;

/**
 * Copies a link in one click. Without the Clipboard API, or when it refuses, a read-only
 * field holding the link opens, selected, for the reader to copy it. Its texts are the
 * caller's: a shared build and a filtered list do not word it the same.
 */
@Component({
  selector: 'lodb-copy-link',
  imports: [Button, Field],
  template: `
    <button type="button" lodbButton="ghost" aria-live="polite" (click)="copy()">
      {{ copied() ? labels().copied : labels().copy }}
    </button>
    @if (fallback()) {
      <label class="w-full">
        <span class="sr-only">{{ labels().error }}</span>
        <input
          #field
          lodbField
          class="font-mono text-xs"
          type="text"
          readonly
          [value]="url()"
          [title]="labels().error"
          (focus)="field.select()"
        />
      </label>
    }
  `,
  host: {
    class: 'flex flex-col gap-2',
    '[class.items-start]': '!stretch()',
    '[class.items-stretch]': 'stretch()',
  },
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CopyLink {
  /** The absolute link. */
  readonly url = input.required<string>();
  /** The button's text, its text once copied, and the name of the fallback field. */
  readonly labels = input.required<{ copy: string; copied: string; error: string }>();
  /** The button fills the width of its box, such as the foot of a panel. */
  readonly stretch = input(false, { transform: booleanAttribute });

  private readonly injector = inject(Injector);
  private readonly field = viewChild<ElementRef<HTMLInputElement>>('field');
  private timer: ReturnType<typeof setTimeout> | undefined;

  protected readonly copied = signal(false);
  protected readonly fallback = signal(false);

  constructor() {
    inject(DestroyRef).onDestroy(() => clearTimeout(this.timer));
  }

  protected async copy(): Promise<void> {
    const clipboard = globalThis.navigator?.clipboard;
    if (!clipboard?.writeText) {
      this.openFallback();
      return;
    }
    try {
      await clipboard.writeText(this.url());
      this.copied.set(true);
      clearTimeout(this.timer);
      this.timer = setTimeout(() => this.copied.set(false), COPIED_RESET_MS);
    } catch {
      this.openFallback();
    }
  }

  private openFallback(): void {
    this.fallback.set(true);
    // The field renders on the next pass: it is handed over focused and selected then.
    afterNextRender(
      () => {
        const field = this.field()?.nativeElement;
        field?.focus();
        field?.select();
      },
      { injector: this.injector },
    );
  }
}
