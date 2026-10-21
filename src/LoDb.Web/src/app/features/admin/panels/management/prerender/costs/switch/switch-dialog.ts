import { DIALOG_DATA, DialogRef } from '@angular/cdk/dialog';
import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import type { PrerenderReport } from '../../../../../../../core/api/generated/models/prerender-report';
import { Button } from '../../../../../../../ui/controls/button';
import { CopyLink } from '../../../../../../../ui/controls/copy-link';
import { DialogFrame } from '../../../../../../../ui/overlays/dialog-frame';
import { figure } from '../../../../../format/figure';
import { AdminTextPipe } from '../../../../../shared/admin-text-pipe';
import { injectAdminText } from '../../../../../shared/inject-admin-text';
import { switchSnippets } from './switch-snippets';

/** The epoch to switch to, or to retire, and the report the dialog reads it in. */
export interface SwitchData {
  readonly target: number;
  readonly report: PrerenderReport;
}

const SWITCH = 'prerender.switch';
const COPY = 'prerender.common.copy';

/**
 * How to serve an epoch (§ 5.5): what it holds (ready, with what the SSR keeps serving, or
 * covered in part), that nginx switches only at its start, then three steps: the variable to
 * set, copiable; the redeploy; and the retirement of the epoch served now, in words only. A
 * retirement rule pasted before the redeploy would delete the epoch nginx still serves: its
 * variables are offered by « Retirer… » on that epoch's row once it is retired.
 */
@Component({
  selector: 'lodb-switch-dialog',
  imports: [AdminTextPipe, Button, CopyLink, DialogFrame],
  templateUrl: './switch-dialog.html',
  host: { class: 'admin-overlay block' },
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class SwitchDialog {
  static readonly HEADING_ID = 'prerender-switch-title';

  protected readonly ref = inject(DialogRef);
  protected readonly data = inject<SwitchData>(DIALOG_DATA);
  protected readonly headingId = SwitchDialog.HEADING_ID;

  private readonly texts = injectAdminText();

  protected readonly snippets = switchSnippets(this.data.target, this.data.report);
  protected readonly copyLabels = computed(() => ({
    copy: this.texts.text(`${COPY}.copy`),
    copied: this.texts.text(`${COPY}.copied`),
    error: this.texts.text(`${COPY}.error`),
  }));
  protected readonly stateLines = computed(() => {
    const state = this.snippets.state;
    if (state?.kind === 'ready') {
      const { versions, failedUnits: units, failedPages: pages } = state;
      const ready = this.texts.text(`${SWITCH}.ready`, { versions });
      if (units === 0 && pages === 0) {
        return [ready];
      }
      const both = units > 0 && pages > 0 ? 'yes' : 'no';
      return [ready, this.texts.text(`${SWITCH}.ready_ssr`, { units, pages, both })];
    }
    return state
      ? [
          this.texts.text(`${SWITCH}.partial`, {
            share: figure(state.share, 'share'),
            missing: state.missing,
          }),
        ]
      : [];
  });
}
