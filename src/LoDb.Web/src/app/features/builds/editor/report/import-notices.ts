import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { TranslocoPipe } from '@jsverse/transloco';
import { EDITOR_ENTRY } from '../form/editor-entry-token';
import { reportNoticesOf } from './report-notices-of';

/**
 * What an import changed, above the form it filled: the patch it landed on, then each
 * thing to review before saving. Nothing outside an import.
 */
@Component({
  selector: 'lodb-import-notices',
  imports: [TranslocoPipe],
  template: `
    @if (notices.length > 0) {
      <ul class="import-notices" role="status">
        @for (notice of notices; track notice.key; let first = $first) {
          <li class="import-notice" [class.import-notice--warn]="!first">
            {{ notice.key | transloco: notice.params }}
          </li>
        }
      </ul>
    }
  `,
  styles: `
    .import-notices {
      display: flex;
      flex-direction: column;
      gap: 0.5rem;
    }
    .import-notice {
      border: 1px solid color-mix(in srgb, var(--color-hex) 45%, transparent);
      background: color-mix(in srgb, var(--color-hex) 8%, transparent);
      padding: 0.7rem 1rem;
      font-size: 0.9rem;
      color: var(--color-text);
    }
    .import-notice--warn {
      border-color: color-mix(in srgb, var(--color-gold) 55%, transparent);
      background: color-mix(in srgb, var(--color-gold) 10%, transparent);
    }
  `,
  host: { class: 'block' },
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ImportNotices {
  private readonly entry = inject(EDITOR_ENTRY);
  protected readonly notices =
    this.entry.report === null
      ? []
      : reportNoticesOf(this.entry.report, this.entry.draft.gameVersion);
}
