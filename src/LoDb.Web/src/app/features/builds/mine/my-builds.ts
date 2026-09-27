import { Location } from '@angular/common';
import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import type { MyBuildRow } from '../../../core/api/generated/models/my-build-row';
import { PageDirection } from '../../../core/layout/direction/page-direction';
import { localePath } from '../../../core/layout/shell/locale-path';
import { Button } from '../../../ui/controls/button';
import { DialogService } from '../../../ui/overlays/dialog-service';
import { DeleteBuildDialog } from './delete-build-dialog';
import { MyBuildsStore } from './my-builds-store';
import { BuildRow } from './row/build-row';

/**
 * The builds of the account, `/{locale}/account/builds`: a row per build with its import,
 * edit and delete, and the way to forge a new one; with none yet, the site's empty state,
 * its way back and home, then the way to forge the first one.
 */
@Component({
  selector: 'lodb-my-builds',
  imports: [BuildRow, Button, RouterLink, TranslocoPipe],
  templateUrl: './my-builds.html',
  providers: [MyBuildsStore],
  host: { class: 'block' },
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class MyBuilds {
  protected readonly store = inject(MyBuildsStore);
  private readonly dialogs = inject(DialogService);
  private readonly location = inject(Location);
  private readonly locale = inject(PageDirection).locale();
  protected readonly newLink = localePath(this.locale, 'account/builds/new');
  protected readonly homeLink = localePath(this.locale, '');

  protected back(): void {
    this.location.back();
  }

  protected confirmDelete(row: MyBuildRow): void {
    const ref = this.dialogs.open<DeleteBuildDialog, string, boolean>(DeleteBuildDialog, {
      labelledBy: DeleteBuildDialog.HEADING_ID,
      data: row.name,
    });
    ref.closed.subscribe((confirmed) => {
      if (confirmed === true) {
        void this.store.remove(row);
      }
    });
  }

  protected importTo(row: MyBuildRow, version: string): void {
    void this.store.importTo(row, version);
  }
}
