import { ChangeDetectionStrategy, Component, input, model } from '@angular/core';
import { AdminTextPipe } from '../shared/admin-text-pipe';

// The legacy reveal toggle: a small outlined pill, lit cyan on hover.
const TOGGLE = [
  'mt-[0.9rem] inline-flex cursor-pointer items-center border border-gold/28 px-[0.7rem]',
  'py-[0.32rem] font-beaufort text-[0.66rem] tracking-[0.14em] text-text-muted uppercase',
  'transition-colors hover:border-hex/50 hover:text-hex',
].join(' ');

/**
 * The toggle under a long list, a ranking or a table, the legacy `collapse_toggle`: "+ 4 de
 * plus" while rows are folded away, "Réduire" once they show. Nothing when none is folded.
 */
@Component({
  selector: 'lodb-fold-toggle',
  imports: [AdminTextPipe],
  template: `
    @if (folded() > 0) {
      <button type="button" [class]="toggle" [attr.aria-expanded]="open()" (click)="flip()">
        {{
          open()
            ? ('admin.rank.less' | adminText)
            : ('admin.rank.more' | adminText: { count: folded() })
        }}
      </button>
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class FoldToggle {
  /** How many rows the list folds away. */
  readonly folded = input.required<number>();
  /** Whether they show. */
  readonly open = model(false);

  protected readonly toggle = TOGGLE;

  protected flip(): void {
    this.open.update((open) => !open);
  }
}
