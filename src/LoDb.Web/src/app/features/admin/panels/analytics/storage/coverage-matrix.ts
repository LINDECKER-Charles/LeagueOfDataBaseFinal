import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import type { VersionCoverage } from '../../../../../core/api/generated/models/version-coverage';
import { AdminTextPipe } from '../../../shared/admin-text-pipe';
import { foldRows } from '../../../widgets/fold-rows';
import { FoldToggle } from '../../../widgets/fold-toggle';

// The legacy matrix showed six versions before its toggle.
const LIMIT = 6;

/**
 * The completeness of each stored version, the legacy matrix: the version, then one chip per
 * language ingested for it, six versions before the rest folds away.
 */
@Component({
  selector: 'lodb-coverage-matrix',
  imports: [FoldToggle, AdminTextPipe],
  template: `
    @if (coverage().length === 0) {
      <p class="text-text-dim">{{ 'admin.storage.coverage_empty' | adminText }}</p>
    } @else {
      <ul class="flex flex-col gap-[0.55rem]">
        @for (row of fold.shown(); track row.version) {
          <li class="grid grid-cols-[7rem_1fr] items-center gap-[0.8rem]">
            <span class="font-mono text-[0.82rem] text-gold-bright">{{ row.version }}</span>
            <span class="flex flex-wrap gap-1">
              @for (lang of row.langs; track lang) {
                <span [class]="chip">{{ lang }}</span>
              }
            </span>
          </li>
        }
      </ul>
      <lodb-fold-toggle [folded]="fold.folded()" [(open)]="fold.open" />
    }
  `,
  host: { class: 'block' },
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CoverageMatrix {
  readonly coverage = input.required<readonly VersionCoverage[]>();

  // Spelled out whole for the Tailwind scanner: the legacy `.lang` chip.
  protected readonly chip = [
    'border border-gold/16 px-[0.35rem] py-[0.1rem]',
    'font-mono text-[0.62rem] text-text-muted uppercase',
  ].join(' ');
  protected readonly fold = foldRows(
    () => this.coverage(),
    () => LIMIT,
  );
}
