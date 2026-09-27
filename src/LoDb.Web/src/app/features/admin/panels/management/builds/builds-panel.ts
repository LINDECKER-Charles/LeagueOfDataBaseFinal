import { ChangeDetectionStrategy, Component } from '@angular/core';
import { RouterLink } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { deleteAdminBuild } from '../../../../../core/api/generated/fn/admin-builds/delete-admin-build';
import { searchAdminBuilds } from '../../../../../core/api/generated/fn/admin-builds/search-admin-builds';
import { unpublishAdminBuild } from '../../../../../core/api/generated/fn/admin-builds/unpublish-admin-build';
import type { AdminBuildRow } from '../../../../../core/api/generated/models/admin-build-row';
import { Button } from '../../../../../ui/controls/button';
import { Field } from '../../../../../ui/controls/field';
import { FigurePipe } from '../../../format/figure-pipe';
import { StampPipe } from '../../../format/stamp-pipe';
import { formText } from '../../../shared/form-text';
import { injectRowActions } from '../../../shared/http/inject-row-actions';
import { injectAdminText } from '../../../shared/inject-admin-text';
import { injectQuery } from '../../../shared/inject-query';
import { injectPanel } from '../../../state/inject-panel';
import { PanelState } from '../../../state/panel-state';
import { AdminPager } from '../../../widgets/admin-pager';
import { Badge } from '../../../widgets/badge';
import { ConfirmButton } from '../../../widgets/confirm-button';
import { Kpi } from '../../../widgets/kpi';
import { PageHead } from '../../../widgets/page-head';

/** The visibilities a build has, as the API filters them. */
const VISIBILITIES = ['public', 'private'] as const;

/**
 * `/admin/builds`: the builds of every account, searched by name, champion or author and
 * filtered by visibility. An administrator takes a public build off the public pages or
 * deletes it.
 */
@Component({
  selector: 'lodb-builds-panel',
  imports: [
    AdminPager,
    Badge,
    Button,
    ConfirmButton,
    Field,
    FigurePipe,
    Kpi,
    PageHead,
    PanelState,
    RouterLink,
    StampPipe,
    TranslocoPipe,
  ],
  templateUrl: './builds-panel.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class BuildsPanel {
  protected readonly texts = injectAdminText();
  protected readonly query = injectQuery();
  protected readonly visibilities = VISIBILITIES;
  protected readonly builds = injectPanel(searchAdminBuilds, () => ({
    q: this.query.text('q') || undefined,
    visibility: this.query.text('visibility') || undefined,
    page: this.query.page(),
  }));
  protected readonly actions = injectRowActions(() => this.builds.reload());

  protected search(event: Event): void {
    this.query.set({
      q: formText(event, 'q').trim() || null,
      visibility: formText(event, 'visibility') || null,
    });
  }

  protected unpublish(build: AdminBuildRow): void {
    void this.actions.run(build.id, {
      call: unpublishAdminBuild,
      params: { id: build.id },
      done: { key: 'builds.done.unpublish', params: { name: build.name } },
    });
  }

  protected remove(build: AdminBuildRow): void {
    void this.actions.run(build.id, {
      call: deleteAdminBuild,
      params: { id: build.id },
      done: { key: 'builds.done.delete', params: { name: build.name } },
    });
  }
}
