import { ChangeDetectionStrategy, Component, computed } from '@angular/core';
import { RouterLink } from '@angular/router';
import { deleteAdminBuild } from '../../../../../core/api/generated/fn/admin-builds/delete-admin-build';
import { searchAdminBuilds } from '../../../../../core/api/generated/fn/admin-builds/search-admin-builds';
import { unpublishAdminBuild } from '../../../../../core/api/generated/fn/admin-builds/unpublish-admin-build';
import type { AdminBuildRow } from '../../../../../core/api/generated/models/admin-build-row';
import { Button } from '../../../../../ui/controls/button';
import { Chip } from '../../../../../ui/controls/chip';
import { FigurePipe } from '../../../format/figure-pipe';
import { StampPipe } from '../../../format/stamp-pipe';
import { AdminCard } from '../../../layout/admin-card';
import { AdminRule } from '../../../layout/admin-rule';
import { PageHead } from '../../../layout/page-head';
import { AdminTextPipe } from '../../../shared/admin-text-pipe';
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

/** The visibilities a build has, as the API filters them. */
const VISIBILITIES = ['public', 'private'] as const;

/**
 * `/admin/builds`: the builds of every account in the legacy table, searched by name or
 * champion and filtered by visibility. An administrator opens a build's share page, takes a
 * public build off the public pages or deletes it.
 */
@Component({
  selector: 'lodb-builds-panel',
  imports: [
    AdminCard,
    AdminPager,
    AdminRule,
    Badge,
    Button,
    Chip,
    ConfirmButton,
    FigurePipe,
    Kpi,
    PageHead,
    PanelState,
    RouterLink,
    StampPipe,
    AdminTextPipe,
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
  /** Whether a search or a visibility narrows the list. */
  protected readonly filtered = computed(
    () => this.query.text('q') !== '' || this.query.text('visibility') !== '',
  );

  /** The score of a build, signed when it is positive, as the legacy table wrote it. */
  protected scoreOf(build: AdminBuildRow): string {
    return build.score > 0 ? `+${build.score}` : String(build.score);
  }

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
