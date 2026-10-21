import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { Button } from '../../../../../../../ui/controls/button';
import { AdminTextPipe } from '../../../../../shared/admin-text-pipe';
import type { SelectionAction } from '../../common/selection/patch-selection';

/**
 * « Prioritaires, dans l'ordre » (§ 3.3.6): the versions rendered first, ranked, each moved
 * up, down or out by buttons named in full (« Monter 16.12.1 »). The API renders them first
 * in this order, then the others newest first. Folded under a summary, which a phone shows
 * as « Prioritaires (3) ».
 */
@Component({
  selector: 'lodb-priority-tray',
  imports: [AdminTextPipe, Button],
  template: `
    <details class="mt-4" open data-priority-tray>
      <summary class="readout-label cursor-pointer">
        {{ 'admin.prerender.launcher.priority.title' | adminText }} ({{ priority().length }})
      </summary>
      @if (priority().length === 0) {
        <p class="readout-text mt-2">{{ 'admin.prerender.launcher.priority.empty' | adminText }}</p>
      } @else {
        <ol class="mt-2 flex flex-wrap gap-x-5 gap-y-2">
          @for (version of priority(); track version; let rank = $index; let isLast = $last) {
            <li class="inline-flex items-center gap-1.5" [attr.data-priority]="version">
              <span class="font-mono text-[0.82rem] text-text-dim">{{ rank + 1 }}.</span>
              <span class="font-mono text-gold-bright">{{ version }}</span>
              <button
                lodbButton="ghost"
                lodbButtonSize="small"
                type="button"
                [disabled]="rank === 0"
                [attr.aria-label]="'admin.prerender.launcher.priority.up' | adminText: { version }"
                (click)="acted.emit({ kind: 'move', version, by: -1 })"
              >
                ↑
              </button>
              <button
                lodbButton="ghost"
                lodbButtonSize="small"
                type="button"
                [disabled]="isLast"
                [attr.aria-label]="
                  'admin.prerender.launcher.priority.down' | adminText: { version }
                "
                (click)="acted.emit({ kind: 'move', version, by: 1 })"
              >
                ↓
              </button>
              <button
                lodbButton="ghost"
                lodbButtonSize="small"
                type="button"
                [attr.aria-label]="
                  'admin.prerender.launcher.priority.remove' | adminText: { version }
                "
                (click)="acted.emit({ kind: 'priority', version })"
              >
                ×
              </button>
            </li>
          }
        </ol>
      }
    </details>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PriorityTray {
  /** The versions rendered first, in this order. */
  readonly priority = input.required<readonly string[]>();
  readonly acted = output<SelectionAction>();
}
