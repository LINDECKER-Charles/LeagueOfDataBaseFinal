import { Location } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { PageDirection } from '../../../../core/layout/direction/page-direction';
import { localePath } from '../../../../core/layout/shell/locale-path';
import { Button } from '../../../../ui/controls/button';
import { FRAME_STYLES } from '../../../../ui/surfaces/frame-styles';

/**
 * What a list says when its version holds no entry at all, such as the runes of a patch
 * older than Runes Reforged: the framed "no results" of the legacy lists, with a way back
 * and a way home. A list whose filters leave nothing says so itself, with a way to clear.
 */
@Component({
  selector: 'lodb-catalogue-empty',
  imports: [Button, RouterLink, TranslocoPipe],
  template: `<p class="mb-3 eyebrow">{{ 'common.no_result.text' | transloco }}</p>
    <div class="mt-6 flex items-center justify-center gap-3">
      <button type="button" lodbButton="ghost" (click)="back()">
        {{ 'common.no_result.back' | transloco }}
      </button>
      <a lodbButton [routerLink]="home()">{{ 'header.navigation.home' | transloco }}</a>
    </div>`,
  host: { class: `${FRAME_STYLES.plain} block p-10 text-center` },
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CatalogueEmpty {
  private readonly location = inject(Location);
  private readonly page = inject(PageDirection);

  protected readonly home = computed(() => localePath(this.page.locale(), ''));

  protected back(): void {
    this.location.back();
  }
}
