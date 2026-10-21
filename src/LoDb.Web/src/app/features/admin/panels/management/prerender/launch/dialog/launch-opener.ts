import { Injectable, inject } from '@angular/core';
import { TranslocoService } from '@jsverse/transloco';
import { firstValueFrom } from 'rxjs';
import type { PrerenderPassStarted } from '../../../../../../../core/api/generated/models/prerender-pass-started';
import type { PrerenderReport } from '../../../../../../../core/api/generated/models/prerender-report';
import { AdminNotices } from '../../../../../layout/admin-notices';
import { ADMIN_I18N } from '../../../../../shared/admin-i18n';
import type { LaunchPreset } from '../../common/scope/launch-preset';
import { injectAdminDialogs } from '../../dialogs/inject-admin-dialogs';
import { LaunchDialog } from './launch-dialog';
import type { LaunchData } from './launch-dialog-store';

const LAUNCHER = 'prerender.launcher';
const LIST_SEPARATOR = ', ';

/**
 * Opens the launch dialog (§ 3.1): a wide dialog, a bottom sheet under 640px. The page alone
 * calls it, with the preset of the entry the operator used. Once a pass starts it says so in
 * the band of the page, with the versions the API left out.
 */
@Injectable({ providedIn: 'root' })
export class LaunchOpener {
  private readonly dialogs = injectAdminDialogs();
  private readonly notices = inject(AdminNotices);
  private readonly transloco = inject(TranslocoService);

  /** Opens the launch dialog; the pass it started, or null once closed. Posts the notice. */
  async open(preset: LaunchPreset, report: PrerenderReport): Promise<PrerenderPassStarted | null> {
    const ref = this.dialogs.open<LaunchDialog, LaunchData, PrerenderPassStarted | null>(
      LaunchDialog,
      { labelledBy: LaunchDialog.HEADING_ID, data: { preset, report }, size: 'wide' },
    );
    const started = (await firstValueFrom(ref.closed)) ?? null;
    if (started) {
      this.notices.post({ tone: 'notice', text: await this.doneText(started) });
    }
    return started;
  }

  private async doneText(started: PrerenderPassStarted): Promise<string> {
    const { id, epoch, units, skippedUnits: skipped, ignoredVersions: ignored } = started;
    const done = await this.text('done', { id, epoch, units, skipped });
    if (ignored.length === 0) {
      return done;
    }
    const versions = ignored.join(LIST_SEPARATOR);
    return `${done} ${await this.text('done_ignored', { count: ignored.length, versions })}`;
  }

  private text(key: string, params: Readonly<Record<string, unknown>>): Promise<string> {
    return firstValueFrom(
      this.transloco.selectTranslate<string>(`${LAUNCHER}.${key}`, params, ADMIN_I18N.path),
    );
  }
}
