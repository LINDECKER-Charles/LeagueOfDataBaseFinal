import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  type ElementRef,
  afterNextRender,
  inject,
  input,
  signal,
  viewChild,
  Injector,
} from '@angular/core';
import { TranslocoPipe } from '@jsverse/transloco';
import { Button } from '../../../../ui/controls/button';
import { Field } from '../../../../ui/controls/field';

const COPIED_RESET_MS = 2000;

/**
 * Copies the link of a shared build in one click. Without the Clipboard API, or when it
 * refuses, a read-only field holding the link opens, selected, for the reader to copy it.
 */
@Component({
  selector: 'lodb-copy-link',
  imports: [Button, Field, TranslocoPipe],
  template: `
    <button type="button" lodbButton="ghost" aria-live="polite" (click)="copy()">
      {{ (copied() ? 'build.show.copied' : 'build.show.copy') | transloco }}
    </button>
    @if (fallback()) {
      <label class="w-full">
        <span class="sr-only">{{ 'build.show.copy_error' | transloco }}</span>
        <input
          #field
          lodbField
          class="font-mono text-xs"
          type="text"
          readonly
          [value]="url()"
          [title]="'build.show.copy_error' | transloco"
          (focus)="field.select()"
        />
      </label>
    }
  `,
  host: { class: 'flex flex-col items-start gap-2' },
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CopyLink {
  /** The absolute link. */
  readonly url = input.required<string>();

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
