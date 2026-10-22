import { ChangeDetectionStrategy, Component, computed } from '@angular/core';
import { RouterLink } from '@angular/router';
import { provideTranslocoScope, TranslocoPipe } from '@jsverse/transloco';
import type { PatchChanges } from '../../../core/api/generated/models/patch-changes';
import type { CatalogueEntry } from '../../../core/routing/catalogue/catalogue-entry';
import { injectRouteData } from '../../../core/routing/inject-route-data';
import { CatalogueImage } from '../../../ui/cards/catalogue-image';
import { Backdrop } from '../../../ui/surfaces/backdrop';
import { injectStateLink } from '../shared/codex/state/links/inject-state-link';
import { injectTranslate } from '../shared/codex/texts/inject-translate';
import { CHANGES_SCOPE } from './changes-scope';
import { applyPatchChangesHead } from './head/apply-patch-changes-head';
import { patchSectionsOf } from './sections/patch-sections-of';

/** Box of an entry's icon, in CSS pixels. */
const ENTRY_ICON_SIZE = 40;

/**
 * `/{locale}/[{version}/]changes`: what a patch changed against the version listed before it,
 * by resource, each entity linking its page at that patch (the opener of its last state for
 * one the patch removed), the fields a change touched named. Rendered by the server from one
 * answer of the API, whose changes the index of the entities' states decides.
 */
@Component({
  selector: 'lodb-patch-changes-page',
  imports: [Backdrop, CatalogueImage, RouterLink, TranslocoPipe],
  providers: [provideTranslocoScope(CHANGES_SCOPE)],
  templateUrl: './patch-changes-page.html',
  host: { class: 'relative isolate block flex-1 px-6 py-12' },
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PatchChangesPage {
  private readonly entry = injectRouteData<CatalogueEntry<PatchChanges>>('changes');
  private readonly translate = injectTranslate();
  private readonly linkTo = injectStateLink();
  protected readonly iconSize = ENTRY_ICON_SIZE;
  protected readonly changes = computed(() => this.entry().details);
  protected readonly locale = computed(() => this.entry().context.locale);
  protected readonly sections = computed(() => {
    const { context, details } = this.entry();
    const format = { locale: context.locale, translate: this.translate() };
    return patchSectionsOf(details.groups, format).map((section) => ({
      ...section,
      groups: section.groups.map((group) => ({
        ...group,
        entries: group.entries.map((entry) => ({
          ...entry,
          url: this.linkTo(context, entry.link),
        })),
      })),
    }));
  });

  constructor() {
    applyPatchChangesHead(this.entry);
  }
}
