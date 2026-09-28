import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { TranslocoPipe } from '@jsverse/transloco';
import { Button } from '../../../../ui/controls/button';
import type { CatalogStatus } from './catalog-status';

/**
 * The three states of a picker section, drawn alike by the champions, the runes and the
 * armory: a loading line, a failure with a retry, else the section's own content.
 */
@Component({
  selector: 'lodb-catalog-state',
  imports: [Button, TranslocoPipe],
  template: `
    @switch (status()) {
      @case ('loading') {
        <p class="font-mono text-[0.7rem] tracking-[0.06em] text-text-dim" role="status">
          {{ 'build.editor.loading' | transloco }}
        </p>
      }
      @case ('error') {
        <div class="catalog-error" role="alert">
          <span>{{ 'build.editor.error' | transloco }}</span>
          <button lodbButton="ghost" type="button" (click)="retry.emit()">
            {{ 'build.editor.retry' | transloco }}
          </button>
        </div>
      }
      @default {
        <ng-content />
      }
    }
  `,
  styles: `
    .catalog-error {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      gap: 0.75rem;
      border: 1px solid color-mix(in srgb, var(--color-danger) 55%, transparent);
      background: color-mix(in srgb, var(--color-danger) 12%, transparent);
      padding: 0.7rem 1rem;
      font-size: 0.85rem;
      color: var(--color-text);
    }
  `,
  host: { class: 'block' },
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CatalogState {
  readonly status = input.required<CatalogStatus>();
  readonly retry = output();
}
