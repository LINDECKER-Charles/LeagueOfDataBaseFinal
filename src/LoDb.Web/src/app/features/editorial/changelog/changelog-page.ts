import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { TranslocoPipe } from '@jsverse/transloco';
import { PageDirection } from '../../../core/layout/direction/page-direction';
import { injectRouteData } from '../../../core/routing/inject-route-data';
import { breadcrumbList } from '../../../core/seo/json-ld/site/breadcrumb-list';
import { applyEditorialHead } from '../shared/apply-editorial-head';
import { ChangelogReader } from './changelog-reader';
import { ChangelogReleaseItem } from './changelog-release-item';
import { releaseAnchor } from './release-anchor';

/**
 * The player-facing changelog: the published releases as a timeline, newest first and open,
 * and the current release. Read from the changelog files of the build, never from the API,
 * so the page prerenders for every locale.
 */
@Component({
  selector: 'lodb-changelog-page',
  imports: [ChangelogReleaseItem, TranslocoPipe],
  templateUrl: './changelog-page.html',
  styleUrl: './changelog-page.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ChangelogPage {
  private readonly reader = inject(ChangelogReader);
  protected readonly heading = injectRouteData<string>('heading');
  protected readonly locale = inject(PageDirection).locale;
  /** `undefined` until the files are read: the empty state is not shown meanwhile. */
  protected readonly releases = toSignal(this.reader.releases());
  protected readonly version = toSignal(this.reader.latestVersion(), { initialValue: null });
  protected readonly anchor = releaseAnchor;

  constructor() {
    applyEditorialHead([], (translate) => ({
      title: translate('changelog.meta.title'),
      description: translate('changelog.meta.description'),
      path: 'changelog',
      jsonLd: (urls) => [
        breadcrumbList([
          { name: translate('header.navigation.home'), url: urls.page('') },
          { name: translate('changelog.title'), url: urls.canonical },
        ]),
      ],
    }));
  }
}
