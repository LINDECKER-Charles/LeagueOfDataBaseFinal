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
import { TranslocoPipe } from '@jsverse/transloco';
import { Button } from '../../../ui/controls/button';
import type { ButtonTone } from '../../../ui/controls/button-tones';

/**
 * An action that asks twice: the first press arms it and offers to confirm or cancel, the
 * second one runs it. It stands where the legacy admin asked with `confirm()`, in the
 * design system and within reach of a keyboard: the focus moves to the confirmation.
 */
@Component({
  selector: 'lodb-confirm-button',
  imports: [Button, TranslocoPipe],
  template: `
    @if (armed()) {
      <span class="inline-flex flex-wrap items-center gap-1.5">
        <button #confirmation lodbButton="gold" type="button" [disabled]="busy()" (click)="run()">
          {{ confirmLabel() }}
        </button>
        <button lodbButton="ghost" type="button" (click)="armed.set(false)">
          {{ 'admin.actions.cancel' | transloco }}
        </button>
      </span>
    } @else {
      <button [lodbButton]="tone()" type="button" [disabled]="busy()" (click)="arm()">
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
