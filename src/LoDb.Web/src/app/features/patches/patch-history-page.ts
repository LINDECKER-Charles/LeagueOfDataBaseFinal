import { ChangeDetectionStrategy, Component, ViewEncapsulation, computed } from '@angular/core';
import { RouterLink } from '@angular/router';
import { provideTranslocoScope, TranslocoPipe } from '@jsverse/transloco';
import { injectRouteData } from '../../core/routing/inject-route-data';
import { Backdrop } from '../../ui/surfaces/backdrop';
import type { PatchHistory } from './data/patch-history';
import { applyPatchHistoryHead } from './head/apply-patch-history-head';
import { PATCHES_SCOPE } from './patches-scope';
import { injectListLabels } from './rows/inject-list-labels';
import { injectPatchGroups } from './rows/inject-patch-groups';

/**
 * `/{locale}/patches`: every version Data Dragon lists, grouped by first segment, newest
 * first, each row linking the four lists of its version. The version selector is a form,
 * which crawlers do not submit: this page and the detail pages' previous patches are how
 * they reach a past patch by links. One read of `/api/meta`, whatever the number of versions.
 */
@Component({
  selector: 'lodb-patch-history-page',
  imports: [Backdrop, RouterLink, TranslocoPipe],
  providers: [provideTranslocoScope(PATCHES_SCOPE)],
  templateUrl: './patch-history-page.html',
  styleUrl: './patch-history-page.css',
  // Four links per version, hundreds of versions: emulation would stamp its attribute on each
  // of their elements, a third of the page's weight. The sheet scopes itself by hand.
  encapsulation: ViewEncapsulation.None,
  host: { class: 'relative isolate block flex-1 px-6 py-12' },
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PatchHistoryPage {
  private readonly history = injectRouteData<PatchHistory>('history');
  protected readonly groups = injectPatchGroups(this.history);
  protected readonly labels = injectListLabels();

  constructor() {
    const locale = computed(() => this.history().locale);
    applyPatchHistoryHead(locale);
  }
}
