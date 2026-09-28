import {
  ChangeDetectionStrategy,
  Component,
  type ElementRef,
  Injector,
  afterNextRender,
  inject,
  input,
  output,
  signal,
  viewChild,
} from '@angular/core';
import { Button } from '../../../ui/controls/button';
import type { ButtonSize } from '../../../ui/controls/button-sizes';
import type { ButtonTone } from '../../../ui/controls/button-tones';
import { AdminTextPipe } from '../shared/admin-text-pipe';

/**
 * An action that asks twice: the first press arms it and offers to confirm or cancel, the
 * second one runs it. It stands where the legacy admin asked with `confirm()`, in the
 * design system and within reach of a keyboard: the focus moves to the confirmation. Small by
 * default, as the actions of a row or a toolbar; a destructive action confirms in red.
 */
@Component({
  selector: 'lodb-confirm-button',
  imports: [Button, AdminTextPipe],
  template: `
    @if (armed()) {
      <span class="inline-flex flex-wrap items-center gap-1.5">
        <button
          #confirmation
          type="button"
          [lodbButton]="confirmTone()"
          [lodbButtonSize]="size()"
          [disabled]="busy()"
          (click)="run()"
        >
          {{ confirmLabel() }}
        </button>
        <button
          lodbButton="ghost"
          type="button"
          [lodbButtonSize]="size()"
          (click)="armed.set(false)"
        >
          {{ 'admin.actions.cancel' | adminText }}
        </button>
      </span>
    } @else {
      <button
        type="button"
        [lodbButton]="tone()"
        [lodbButtonSize]="size()"
        [disabled]="busy()"
        (click)="arm()"
      >
        {{ label() }}
      </button>
    }
  `,
  host: { class: 'inline-block' },
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ConfirmButton {
  /** What the action is called, translated: "Supprimer". */
  readonly label = input.required<string>();
  /** What confirms it, translated: "Supprimer définitivement". */
  readonly confirmLabel = input.required<string>();
  readonly tone = input<ButtonTone>('ghost');
  /** The tone of the confirmation: `danger` for what cannot be undone. */
  readonly confirmTone = input<ButtonTone>('gold');
  readonly size = input<ButtonSize>('small');
  readonly busy = input(false);
  readonly confirmed = output<void>();

  private readonly injector = inject(Injector);
  private readonly confirmation = viewChild<ElementRef<HTMLButtonElement>>('confirmation');
  protected readonly armed = signal(false);

  protected arm(): void {
    this.armed.set(true);
    afterNextRender(() => this.confirmation()?.nativeElement.focus(), {
      injector: this.injector,
    });
  }

  protected run(): void {
    this.armed.set(false);
    this.confirmed.emit();
  }
}
