import { DIALOG_DATA, DialogRef } from '@angular/cdk/dialog';
import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { Button } from '../../../../../../../ui/controls/button';
import { CopyLink } from '../../../../../../../ui/controls/copy-link';
import { DialogFrame } from '../../../../../../../ui/overlays/dialog-frame';
import { AdminTextPipe } from '../../../../../shared/admin-text-pipe';
import { injectAdminText } from '../../../../../shared/inject-admin-text';
import { monthlyCost } from '../../../backups/monthly-cost';
import type { SwitchData } from './switch-dialog';
import { switchSnippets } from './switch-snippets';

const COPY = 'prerender.common.copy';

/**
 * The retirement of an epoch no longer served (§ 5.5): what it costs until its lifecycle
 * deletes it, then the exact variables to add to the command of tools/prerender/README.md,
 * copiable now that nginx serves another epoch. Without the bucket's name on this host the
 * line keeps a placeholder and cannot be copied whole. On staging the bucket's expiry rule
 * deletes every page anyway.
 */
@Component({
  selector: 'lodb-retire-dialog',
  imports: [AdminTextPipe, Button, CopyLink, DialogFrame],
  template: `
    @let retire = snippets.retire;
    <lodb-dialog
      [heading]="'admin.prerender.switch.retire_title' | adminText: { epoch: snippets.target }"
      [headingId]="headingId"
      [closeLabel]="'admin.prerender.switch.close' | adminText"
    >
      <div class="flex flex-col gap-4 text-[0.9rem] text-text-muted">
        <p class="m-0">{{ body() }}</p>
        @if (retire) {
          <p class="m-0">{{ 'admin.prerender.switch.retire_vars' | adminText }}</p>
          <div class="flex flex-wrap items-center gap-3">
            <code class="font-mono break-all text-gold-bright" data-retire-line>{{
              retire.line
            }}</code>
            @if (retire.canCopy) {
              <lodb-copy-link [url]="retire.line" [labels]="copyLabels()" />
            }
          </div>
          @if (!retire.canCopy) {
            <p class="m-0 text-gold" data-no-bucket>
              {{ 'admin.prerender.switch.retire_no_bucket' | adminText }}
            </p>
          }
        }
        <p class="m-0 text-text-dim">{{ 'admin.prerender.switch.retire_staging' | adminText }}</p>
        <div class="flex justify-end">
          <button lodbButton="ghost" type="button" (click)="ref.close()">
            {{ 'admin.prerender.switch.close' | adminText }}
          </button>
        </div>
      </div>
    </lodb-dialog>
  `,
  host: { class: 'admin-overlay block' },
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class RetireDialog {
  static readonly HEADING_ID = 'prerender-retire-title';

  protected readonly ref = inject(DialogRef);
  protected readonly data = inject<SwitchData>(DIALOG_DATA);
  protected readonly headingId = RetireDialog.HEADING_ID;

  private readonly texts = injectAdminText();

  protected readonly snippets = switchSnippets(this.data.target, this.data.report);
  protected readonly copyLabels = computed(() => ({
    copy: this.texts.text(`${COPY}.copy`),
    copied: this.texts.text(`${COPY}.copied`),
    error: this.texts.text(`${COPY}.error`),
  }));
  protected readonly body = computed(() =>
    this.texts.text('prerender.switch.retire_body', {
      epoch: this.snippets.target,
      served: this.snippets.served,
      cost: monthlyCost(this.snippets.retire?.monthlyCost ?? 0, this.data.report.storage.currency),
    }),
  );
}
