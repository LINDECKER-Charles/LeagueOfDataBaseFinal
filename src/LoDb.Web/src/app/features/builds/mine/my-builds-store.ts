import { HttpErrorResponse, HttpStatusCode } from '@angular/common/http';
import { Injectable, computed, inject, resource } from '@angular/core';
import { Router } from '@angular/router';
import { TranslocoService } from '@jsverse/transloco';
import { firstValueFrom } from 'rxjs';
import type { MyBuildRow } from '../../../core/api/generated/models/my-build-row';
import { BuildsService } from '../../../core/api/generated/services/builds.service';
import { ApiMeta } from '../../../core/api/meta/api-meta';
import { pageContextOf } from '../../../core/context/page-context-of';
import { PreferencesStore } from '../../../core/context/preferences/preferences-store';
import { PageDirection } from '../../../core/layout/direction/page-direction';
import { localePath } from '../../../core/layout/shell/locale-path';
import { ToastService } from '../../../core/layout/toast/toast-service';
import { catalogStatus } from '../editor/catalog/catalog-status';
import { queryOf } from '../editor/entry/query-of';

/** The list of the account's builds, and the patches a build may be imported to. */
interface MyBuildsList {
  readonly rows: readonly MyBuildRow[];
  readonly versions: readonly string[];
}

/**
 * The builds of the signed-in account, read on the patch and the language browsed, the
 * latest change first. A build is deleted once confirmed, then the list is read again; an
 * import opens the editor on the build carried over to the patch picked.
 */
@Injectable()
export class MyBuildsStore {
  private readonly builds = inject(BuildsService);
  private readonly meta = inject(ApiMeta);
  private readonly preferences = inject(PreferencesStore);
  private readonly page = inject(PageDirection);
  private readonly router = inject(Router);
  private readonly toasts = inject(ToastService);
  private readonly transloco = inject(TranslocoService);
  private readonly list = resource({ loader: () => this.load() });

  readonly status = computed(() => catalogStatus(this.list.status()));
  readonly rows = computed(() => (this.list.hasValue() ? this.list.value().rows : []));
  readonly versions = computed(() => (this.list.hasValue() ? this.list.value().versions : []));

  reload(): void {
    this.list.reload();
  }

  async remove(row: MyBuildRow): Promise<void> {
    try {
      await firstValueFrom(this.builds.deleteBuild({ id: row.id }));
      this.toasts.show('success', this.transloco.translate('build.flash.deleted'));
    } catch (error) {
      // A build already gone leaves the list as wished: it is only read again.
      if (!(error instanceof HttpErrorResponse && error.status === HttpStatusCode.NotFound)) {
        this.toasts.show('error', this.transloco.translate('buildsEditor.list.delete_failed'));
      }
    }
    this.list.reload();
  }

  importTo(row: MyBuildRow, version: string): Promise<boolean> {
    const path = localePath(this.page.locale(), `account/builds/${row.id}/import`);
    return this.router.navigate([path], { queryParams: { to: version } });
  }

  private async load(): Promise<MyBuildsList> {
    const meta = await firstValueFrom(this.meta.meta());
    const sources = {
      locale: this.page.locale(),
      path: null,
      query: queryOf(this.router.url),
      remembered: this.preferences.read(),
    };
    const context = pageContextOf(sources, meta);
    const query = context === null ? {} : { version: context.version, lang: context.language };
    const rows = await firstValueFrom(this.builds.listMyBuilds(query));
    return { rows, versions: meta.versions };
  }
}
